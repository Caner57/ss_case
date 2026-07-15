namespace ConfigReader.Core.Domain;

/// <summary>
/// Thrown when a stored <c>Type</c> token cannot be mapped to a known
/// <see cref="ConfigurationType"/>. Signals bad data rather than silently
/// returning a wrong value.
/// </summary>
public sealed class UnknownConfigurationTypeException : Exception
{
    public string? Token { get; }

    public UnknownConfigurationTypeException(string? token)
        : base($"Unknown configuration type token: '{token ?? "<null>"}'.")
    {
        Token = token;
    }
}
