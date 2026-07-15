namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// The only thing that ever crosses the broker: a <em>signal</em> that a record changed for one
/// application. It deliberately carries no <c>Value</c> or any configuration payload — only the
/// owning <see cref="ApplicationName"/> and the changed record's <see cref="RecordId"/>. A
/// subscriber treats this purely as a "re-read from storage now" trigger and never writes any
/// field of it into its cache. Keeping the payload value-free is the core anti cache-poisoning
/// guarantee (CFG-9.3): even an attacker who fully controls the broker can only ask the library to
/// re-read the authoritative value from Mongo, never inject a value of their choosing.
/// </summary>
public sealed record ConfigurationChangeSignal(string ApplicationName, string RecordId);
