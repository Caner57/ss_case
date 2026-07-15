using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>Passes the stored value through unchanged for <see cref="ConfigurationType.String"/>.</summary>
public sealed class StringValueConverter : IValueConverter
{
    public ConfigurationType Type => ConfigurationType.String;

    public object Convert(string rawValue) => rawValue;
}
