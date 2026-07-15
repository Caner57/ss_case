namespace ConfigReader.Admin.Api.Contracts;

/// <summary>
/// Inbound payload to update an existing configuration record. The target <c>Id</c> comes
/// from the route, not the body, so a request can never silently retarget another record.
/// </summary>
public sealed record UpdateConfigurationRequest(
    string Name,
    string Type,
    string Value,
    bool IsActive,
    string ApplicationName);
