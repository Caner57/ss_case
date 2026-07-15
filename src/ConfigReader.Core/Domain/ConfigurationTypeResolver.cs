namespace ConfigReader.Core.Domain;

/// <summary>
/// Resolves a raw <c>Type</c> token (as stored, e.g. "Int", "integer", "boolean")
/// to a <see cref="ConfigurationType"/>. Resolution is case-insensitive and
/// variant-tolerant because the source data mixes spellings ("int"/"integer",
/// "bool"/"boolean").
/// </summary>
public static class ConfigurationTypeResolver
{
    private static readonly IReadOnlyDictionary<string, ConfigurationType> TokenMap =
        new Dictionary<string, ConfigurationType>(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = ConfigurationType.String,
            ["int"] = ConfigurationType.Int,
            ["integer"] = ConfigurationType.Int,
            ["double"] = ConfigurationType.Double,
            ["bool"] = ConfigurationType.Bool,
            ["boolean"] = ConfigurationType.Bool
        };

    public static ConfigurationType Resolve(string token)
    {
        if (TryResolve(token, out var type))
        {
            return type;
        }

        throw new UnknownConfigurationTypeException(token);
    }

    /// <summary>Non-throwing resolution for validation paths that must decide validity without
    /// relying on exceptions for control flow.</summary>
    public static bool TryResolve(string? token, out ConfigurationType type)
    {
        if (!string.IsNullOrWhiteSpace(token) && TokenMap.TryGetValue(token.Trim(), out type))
        {
            return true;
        }

        type = default;
        return false;
    }
}
