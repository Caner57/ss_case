namespace ConfigReader.Core.Domain;

/// <summary>
/// A single configuration entry as it lives in the backing store. Mirrors the
/// case table columns exactly. <see cref="Value"/> is always kept as a string and
/// converted to its real .NET type at read time based on <see cref="Type"/>.
/// </summary>
public sealed class ConfigurationRecord
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Raw type token as stored (e.g. "Int", "boolean"); resolved via <see cref="ConfigurationTypeResolver"/>.</summary>
    public required string Type { get; init; }

    /// <summary>Value kept as string in storage; converted at runtime per <see cref="Type"/>.</summary>
    public required string Value { get; init; }

    public required bool IsActive { get; init; }

    public required string ApplicationName { get; init; }
}
