namespace ConfigReader.Admin.Api.Application;

/// <summary>
/// Append-only port for recording configuration changes. It exposes an insert path only —
/// no update or delete — so the audit trail cannot be tampered with through the application
/// layer. Storage-agnostic by design; the MongoDB adapter lives in the Infrastructure folder.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
