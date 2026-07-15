using System.Globalization;
using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>Parses the stored value as a culture-invariant 32-bit integer.</summary>
public sealed class IntValueConverter : IValueConverter
{
    public ConfigurationType Type => ConfigurationType.Int;

    public object Convert(string rawValue) =>
        int.Parse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
}
