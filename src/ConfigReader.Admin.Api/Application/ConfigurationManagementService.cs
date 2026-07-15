using ConfigReader.Admin.Api.Contracts;
using ConfigReader.Core.Application;
using ConfigReader.Core.Application.Messaging;

namespace ConfigReader.Admin.Api.Application;

/// <summary>
/// Management use-cases behind the CRUD endpoints. The controller stays thin by delegating
/// here; this service owns the orchestration and depends only on the Core ports
/// (<see cref="IConfigurationManagementStore"/> for management reads,
/// <see cref="IConfigurationWriter"/> for writes), never on a concrete storage technology.
/// </summary>
public sealed class ConfigurationManagementService
{
    private readonly IConfigurationManagementStore _store;
    private readonly IConfigurationWriter _writer;
    private readonly IAuditLogWriter _auditLog;
    private readonly ICurrentActor _currentActor;
    private readonly IChangeNotifier _changeNotifier;
    private readonly ILogger<ConfigurationManagementService> _logger;

    public ConfigurationManagementService(
        IConfigurationManagementStore store,
        IConfigurationWriter writer,
        IAuditLogWriter auditLog,
        ICurrentActor currentActor,
        IChangeNotifier changeNotifier,
        ILogger<ConfigurationManagementService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(currentActor);
        ArgumentNullException.ThrowIfNull(changeNotifier);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _writer = writer;
        _auditLog = auditLog;
        _currentActor = currentActor;
        _changeNotifier = changeNotifier;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConfigurationDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _store.GetAllAsync(cancellationToken);
        return records.Select(ConfigurationMapper.ToDto).ToList();
    }

    public async Task<ConfigurationDto?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await _store.GetByIdAsync(id, cancellationToken);
        return record is null ? null : ConfigurationMapper.ToDto(record);
    }

    public async Task<ConfigurationDto> CreateAsync(
        CreateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var record = ConfigurationMapper.ToNewRecord(request);
        var newId = await _writer.AddAsync(record, cancellationToken);

        await RecordAuditAsync(new AuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Actor = _currentActor.Name,
            ApplicationName = request.ApplicationName,
            Name = request.Name,
            OldValue = null,
            NewValue = request.Value,
            Operation = AuditOperation.Create
        }, cancellationToken);

        await PublishChangeSignalAsync(newId, request.ApplicationName, cancellationToken);

        return new ConfigurationDto(
            newId,
            request.Name,
            request.Type,
            request.Value,
            request.IsActive,
            request.ApplicationName);
    }

    public async Task<ConfigurationDto?> UpdateAsync(
        string id,
        UpdateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _store.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var record = ConfigurationMapper.ToUpdatedRecord(id, request);
        await _writer.UpdateAsync(record, cancellationToken);

        await RecordAuditAsync(new AuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Actor = _currentActor.Name,
            ApplicationName = request.ApplicationName,
            Name = request.Name,
            OldValue = existing.Value,
            NewValue = request.Value,
            Operation = AuditOperation.Update
        }, cancellationToken);

        await PublishChangeSignalAsync(id, request.ApplicationName, cancellationToken);

        return ConfigurationMapper.ToDto(record);
    }

    private async Task PublishChangeSignalAsync(
        string recordId,
        string applicationName,
        CancellationToken cancellationToken)
    {
        // Best-effort push (CFG-4.2): the change is already persisted and polling (CFG-3.4) will
        // pick it up regardless. The broker only shaves off the poll-interval latency, so a publish
        // failure must never fail the write — it is logged and swallowed. The payload is a value-free
        // signal (CFG-9.3): subscribers re-read the real value from storage, never from the message.
        try
        {
            var signal = new ConfigurationChangeSignal(applicationName, recordId);
            await _changeNotifier.PublishAsync(signal, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish change signal for {ApplicationName}/{RecordId}; the change is persisted "
                    + "and polling will still deliver it.",
                applicationName,
                recordId);
        }
    }

    private async Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        // Best-effort: the configuration change has already been persisted. An audit failure must
        // never make the change look silently successful, so we surface it loudly in the logs
        // instead of swallowing it (accountability outranks convenience here).
        try
        {
            await _auditLog.WriteAsync(entry, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to write audit entry for {Operation} on {ApplicationName}/{Name} by {Actor}.",
                entry.Operation,
                entry.ApplicationName,
                entry.Name,
                entry.Actor);
        }
    }
}
