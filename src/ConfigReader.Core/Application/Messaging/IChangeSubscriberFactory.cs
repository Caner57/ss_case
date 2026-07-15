namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// Builds an <see cref="IChangeSubscriber"/> without Core referencing a concrete broker, mirroring
/// <see cref="IConfigurationStoreFactory"/>. The factory is pre-configured with the broker
/// connection at the composition root (the reader's public constructor only knows the storage
/// connection string, so the Redis endpoint is orthogonal host configuration baked into the
/// factory), and is registered once via
/// <see cref="ConfigurationReader.UseChangeSubscriberFactory"/>. When no factory is registered the
/// library simply runs on polling only, so the broker never becomes a single point of dependency.
/// </summary>
public interface IChangeSubscriberFactory
{
    IChangeSubscriber Create();
}
