namespace ConfigReader.Core.Domain;

/// <summary>
/// The three sample records from the case, seeded on first startup so the ecosystem
/// has a working demo dataset. Values are written verbatim from the case table (see
/// CFG-2.3): note that <c>MaxItemCount</c> is intentionally inactive so the library's
/// IsActive filter can be demonstrated. Ids are empty — storage assigns them on insert.
/// </summary>
public static class CaseSeedData
{
    public static IReadOnlyList<ConfigurationRecord> Records { get; } = new[]
    {
        new ConfigurationRecord
        {
            Id = string.Empty,
            Name = "SiteName",
            Type = "string",
            Value = "soty.io",
            IsActive = true,
            ApplicationName = "SERVICE-A"
        },
        new ConfigurationRecord
        {
            Id = string.Empty,
            Name = "IsBasketEnabled",
            Type = "bool",
            Value = "1",
            IsActive = true,
            ApplicationName = "SERVICE-B"
        },
        new ConfigurationRecord
        {
            Id = string.Empty,
            Name = "MaxItemCount",
            Type = "int",
            Value = "50",
            IsActive = false,
            ApplicationName = "SERVICE-A"
        }
    };
}
