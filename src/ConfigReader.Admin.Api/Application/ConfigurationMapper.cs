using ConfigReader.Admin.Api.Contracts;
using ConfigReader.Core.Domain;

namespace ConfigReader.Admin.Api.Application;

/// <summary>Translates between the domain <see cref="ConfigurationRecord"/> and the API DTOs,
/// keeping the mapping out of both the controller and the domain model.</summary>
internal static class ConfigurationMapper
{
    public static ConfigurationDto ToDto(ConfigurationRecord record) => new(
        record.Id,
        record.Name,
        record.Type,
        record.Value,
        record.IsActive,
        record.ApplicationName);

    public static ConfigurationRecord ToNewRecord(CreateConfigurationRequest request) => new()
    {
        Id = string.Empty,
        Name = request.Name,
        Type = request.Type,
        Value = request.Value,
        IsActive = request.IsActive,
        ApplicationName = request.ApplicationName
    };

    public static ConfigurationRecord ToUpdatedRecord(string id, UpdateConfigurationRequest request) => new()
    {
        Id = id,
        Name = request.Name,
        Type = request.Type,
        Value = request.Value,
        IsActive = request.IsActive,
        ApplicationName = request.ApplicationName
    };
}
