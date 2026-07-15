namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// Single source of truth for the pub/sub channel naming shared by the publisher (Admin API) and
/// every subscriber (library). Each application gets its own literal channel
/// <c>config-changes:{applicationName}</c>, so one application's change never reaches another
/// application's listeners (isolation, CFG-3.6/CFG-9.3).
/// <para>
/// There is deliberately no API here to build a wildcard/pattern channel (e.g.
/// <c>config-changes:*</c>): a caller can only ever name one concrete application, which makes
/// cross-tenant wildcard subscription impossible by design rather than by convention.
/// </para>
/// </summary>
public static class ConfigurationChangeChannel
{
    public const string Prefix = "config-changes:";

    public static string ForApplication(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        return Prefix + applicationName;
    }
}
