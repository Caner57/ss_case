using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>
/// Converts the stored value to a boolean, accepting both numeric ("1"/"0") and textual
/// ("true"/"false", case-insensitive) forms because the case data mixes them.
/// </summary>
public sealed class BoolValueConverter : IValueConverter
{
    public ConfigurationType Type => ConfigurationType.Bool;

    public object Convert(string rawValue)
    {
        var token = rawValue.Trim();

        return token switch
        {
            "1" => true,
            "0" => false,
            _ when bool.TryParse(token, out var parsed) => parsed,
            _ => throw new FormatException($"Value '{rawValue}' is not a valid boolean.")
        };
    }
}
