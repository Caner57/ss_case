namespace ConfigReader.Admin.Api.Contracts;

/// <summary>
/// Outbound API representation of a configuration record. Mirrors the case table's six
/// columns and is intentionally decoupled from the domain <c>ConfigurationRecord</c> so the
/// API contract can evolve independently of the storage model (DTO ≠ Domain).
/// </summary>
public sealed record ConfigurationDto(
    string Id,
    string Name,
    string Type,
    string Value,
    bool IsActive,
    string ApplicationName);
