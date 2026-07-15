using System.Text.RegularExpressions;
using ConfigReader.Core.Application.TypeConversion;
using ConfigReader.Core.Domain;

namespace ConfigReader.Admin.Api.Application;

/// <summary>
/// Validates create/update payloads before they reach storage so only consistent data is
/// persisted (CFG-5.2). Type/Value semantics are checked with the very same Core components the
/// library uses at read time (<see cref="ConfigurationTypeResolver"/>,
/// <see cref="ValueConverterRegistry"/>), guaranteeing the API and the library never diverge on
/// what a valid type/value is.
/// </summary>
public sealed partial class ConfigurationValidator
{
    public const int MaxValueLength = 4096;

    private static readonly Regex ApplicationNamePattern = BuildApplicationNamePattern();

    private readonly ValueConverterRegistry _converters;

    public ConfigurationValidator(ValueConverterRegistry converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        _converters = converters;
    }

    /// <summary>Returns a field → error-messages map. An empty map means the payload is valid.</summary>
    public IReadOnlyDictionary<string, string[]> Validate(
        string? name,
        string? type,
        string? value,
        string? applicationName)
    {
        var errors = new Dictionary<string, List<string>>();

        ValidateName(name, errors);
        ValidateApplicationName(applicationName, errors);
        ValidateTypeAndValue(type, value, errors);

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    private static void ValidateName(string? name, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Add(errors, nameof(name), "Name is required.");
        }
    }

    private static void ValidateApplicationName(string? applicationName, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            Add(errors, nameof(applicationName), "ApplicationName is required.");
            return;
        }

        if (!ApplicationNamePattern.IsMatch(applicationName))
        {
            Add(errors, nameof(applicationName),
                "ApplicationName must contain only uppercase letters, digits and hyphens (e.g. 'SERVICE-A').");
        }
    }

    private void ValidateTypeAndValue(string? type, string? value, Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            Add(errors, nameof(type), "Type is required.");
            return;
        }

        if (!ConfigurationTypeResolver.TryResolve(type, out var resolvedType))
        {
            Add(errors, nameof(type),
                $"Type '{type}' is not supported. Allowed: string, int/integer, double, bool/boolean.");
            return;
        }

        if (value is { Length: > MaxValueLength })
        {
            Add(errors, nameof(value), $"Value exceeds the maximum length of {MaxValueLength} characters.");
            return;
        }

        if (!IsValueValidForType(resolvedType, value))
        {
            Add(errors, nameof(value), $"Value '{value}' is not a valid {resolvedType} value.");
        }
    }

    private bool IsValueValidForType(ConfigurationType type, string? value)
    {
        try
        {
            _converters.Convert(type, value ?? string.Empty);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            errors[field] = messages;
        }

        messages.Add(message);
    }

    [GeneratedRegex("^[A-Z0-9](?:[A-Z0-9-]*[A-Z0-9])?$")]
    private static partial Regex BuildApplicationNamePattern();
}
