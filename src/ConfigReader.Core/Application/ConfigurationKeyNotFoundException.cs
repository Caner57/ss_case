namespace ConfigReader.Core.Application;

/// <summary>
/// Thrown by <see cref="ConfigurationReader.GetValue{T}"/> when a key is not present in the
/// application's active snapshot. Signalling an explicit, application-scoped miss (rather than
/// returning <c>default</c>) keeps cross-tenant lookups observable: asking for another
/// application's key produces a defined "not found" rather than a silent wrong value.
/// </summary>
public sealed class ConfigurationKeyNotFoundException : Exception
{
    public string Key { get; }

    public string ApplicationName { get; }

    public ConfigurationKeyNotFoundException(string key, string applicationName)
        : base($"Configuration key '{key}' was not found for application '{applicationName}'.")
    {
        Key = key;
        ApplicationName = applicationName;
    }
}
