namespace ConfigReader.Core.Application;

/// <summary>
/// Builds an <see cref="IConfigurationStore"/> from a raw connection string. This port lets
/// <see cref="ConfigurationReader"/> honour the case's one-line
/// <c>new ConfigurationReader(applicationName, connectionString, refreshTimerIntervalInMs)</c>
/// contract without <see cref="ConfigReader.Core"/> ever referencing a concrete storage
/// technology: the real factory lives in the infrastructure layer (ConfigReader.Storage.Mongo)
/// and is registered once at the composition root via
/// <see cref="ConfigurationReader.UseStoreFactory"/>. Dependency direction stays
/// Infrastructure -> Core.
/// </summary>
public interface IConfigurationStoreFactory
{
    IConfigurationStore Create(string connectionString);
}
