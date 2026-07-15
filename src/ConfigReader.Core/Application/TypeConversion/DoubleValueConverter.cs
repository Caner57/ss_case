using System.Globalization;
using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>Parses the stored value as a culture-invariant double so a stored "3.14" never
/// depends on the host's locale.</summary>
public sealed class DoubleValueConverter : IValueConverter
{
    public ConfigurationType Type => ConfigurationType.Double;

    public object Convert(string rawValue) =>
        double.Parse(rawValue.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
}
