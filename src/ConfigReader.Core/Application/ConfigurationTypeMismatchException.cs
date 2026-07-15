namespace ConfigReader.Core.Application;

/// <summary>
/// Thrown by <see cref="ConfigurationReader.GetValue{T}"/> when the caller's requested type
/// <c>T</c> does not match the record's stored type (e.g. asking for an <c>int</c> on a
/// <c>bool</c> field). Failing loudly prevents silently returning a wrong-typed value.
/// </summary>
public sealed class ConfigurationTypeMismatchException : Exception
{
    public string Key { get; }

    public Type RequestedType { get; }

    public Type ActualType { get; }

    public ConfigurationTypeMismatchException(string key, Type requestedType, Type actualType)
        : base($"Configuration key '{key}' is of type '{actualType.Name}' but was requested as '{requestedType.Name}'.")
    {
        Key = key;
        RequestedType = requestedType;
        ActualType = actualType;
    }
}
