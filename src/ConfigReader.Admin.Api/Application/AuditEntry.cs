namespace ConfigReader.Admin.Api.Application;

/// <summary>
/// An immutable record of a single configuration change: who changed which key of which
/// application, when, and from which old value to which new value. Deliberately carries only
/// the config field name/value and the actor identity — never a secret such as a connection
/// string — so the audit trail can be kept and read without leaking credentials.
/// </summary>
public sealed record AuditEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Authenticated identity that performed the change (from CFG-9.1 auth).</summary>
    public required string Actor { get; init; }

    public required string ApplicationName { get; init; }

    /// <summary>The configuration key that was affected.</summary>
    public required string Name { get; init; }

    /// <summary>Previous value; <c>null</c> for a create (there was no prior value).</summary>
    public string? OldValue { get; init; }

    public required string NewValue { get; init; }

    public required AuditOperation Operation { get; init; }
}
