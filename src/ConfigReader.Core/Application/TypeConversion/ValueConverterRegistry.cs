using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.TypeConversion;

/// <summary>
/// Dispatches a raw stored value to the <see cref="IValueConverter"/> strategy for its
/// <see cref="ConfigurationType"/>. This keeps <see cref="ConfigurationReader.GetValue{T}"/> free
/// of any type-switch logic (Strategy pattern, single responsibility): the reader only resolves
/// the record and delegates conversion here.
/// </summary>
public sealed class ValueConverterRegistry
{
    private readonly IReadOnlyDictionary<ConfigurationType, IValueConverter> _converters;

    public ValueConverterRegistry(IEnumerable<IValueConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        _converters = converters.ToDictionary(converter => converter.Type);
    }

    /// <summary>Registry wired with the built-in string/int/double/bool strategies.</summary>
    public static ValueConverterRegistry CreateDefault() => new(new IValueConverter[]
    {
        new StringValueConverter(),
        new IntValueConverter(),
        new DoubleValueConverter(),
        new BoolValueConverter()
    });

    public object Convert(ConfigurationType type, string rawValue)
    {
        if (!_converters.TryGetValue(type, out var converter))
        {
            throw new UnknownConfigurationTypeException(type.ToString());
        }

        return converter.Convert(rawValue);
    }
}
