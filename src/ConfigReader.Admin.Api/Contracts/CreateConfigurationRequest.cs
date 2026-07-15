namespace ConfigReader.Admin.Api.Contracts;

/// <summary>
/// Inbound payload to create a configuration record. The <c>Id</c> is deliberately omitted:
/// it is assigned by the store on insert. Field-level validation is applied in CFG-5.2.
/// </summary>
public sealed record CreateConfigurationRequest(
    string Name,
    string Type,
    string Value,
    bool IsActive,
    string ApplicationName);
