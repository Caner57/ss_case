using System.Text.Json;
using ConfigReader.Core.Application.Messaging;

namespace ConfigReader.Broker.Redis;

/// <summary>
/// The single wire format shared by the publisher (<see cref="RedisChangeNotifier"/>) and the
/// subscriber (<see cref="RedisChangeSubscriber"/>) so both sides agree on the CFG-4.1 contract.
/// Deserialization is deliberately forgiving: a malformed or unexpected payload yields
/// <c>null</c> rather than throwing, so a hostile or garbled broker message can never crash a
/// subscriber (CFG-9.3) — it is simply ignored.
/// </summary>
internal static class ConfigurationChangeSignalSerializer
{
    public static string Serialize(ConfigurationChangeSignal signal) =>
        JsonSerializer.Serialize(signal);

    public static ConfigurationChangeSignal? TryDeserialize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var signal = JsonSerializer.Deserialize<ConfigurationChangeSignal>(payload);
            if (signal is null || string.IsNullOrWhiteSpace(signal.ApplicationName))
            {
                return null;
            }

            return signal;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
