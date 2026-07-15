namespace ConfigReader.Core.Domain;

/// <summary>
/// Semantic value type of a configuration record. The raw <c>Type</c> token stored
/// in the backing store is resolved to one of these via <see cref="ConfigurationTypeResolver"/>.
/// </summary>
public enum ConfigurationType
{
    String,
    Int,
    Double,
    Bool
}
