using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>
/// A per-type conversion strategy. Each implementation converts the raw string
/// <c>Value</c> of a record into its concrete .NET representation for exactly one
/// <see cref="ConfigurationType"/>. Isolating conversion here keeps
/// <see cref="IConfigurationReader.GetValue{T}"/> free of type-switch logic
/// (Strategy pattern). Concrete strategies are implemented in CFG-3.2.
/// </summary>
public interface IValueConverter
{
    /// <summary>The single configuration type this strategy handles.</summary>
    ConfigurationType Type { get; }

    /// <summary>Converts the stored raw value into its boxed .NET value.</summary>
    object Convert(string rawValue);
}
