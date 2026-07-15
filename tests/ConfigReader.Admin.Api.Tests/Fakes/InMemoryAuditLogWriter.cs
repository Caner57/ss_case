using System.Collections.Concurrent;
using ConfigReader.Admin.Api.Application;

namespace ConfigReader.Admin.Api.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAuditLogWriter"/> so audit behaviour can be asserted end-to-end via
/// <c>WebApplicationFactory</c> without a real MongoDB. <see cref="ThrowOnWrite"/> simulates an
/// audit-store outage to prove the failure is logged rather than silently swallowed.
/// </summary>
public sealed class InMemoryAuditLogWriter : IAuditLogWriter
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public bool ThrowOnWrite { get; set; }

    public IReadOnlyList<AuditEntry> Entries => _entries.ToList();

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("Audit store unavailable.");
        }

        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }
}
