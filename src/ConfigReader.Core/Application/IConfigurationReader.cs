namespace ConfigReader.Core.Application;

/// <summary>
/// The single public read surface of the library. Consumers resolve a typed value
/// by key; type conversion happens transparently inside the implementation.
/// </summary>
public interface IConfigurationReader
{
    T GetValue<T>(string key);
}
