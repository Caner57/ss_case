using System.Reflection;
using ConfigReader.Core.Application.Messaging;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application.Messaging;

/// <summary>
/// Code-level guards for the broker security design (CFG-9.3): the library's subscribe surface can
/// only ever bind to one concrete application, and the channel scheme can never yield a wildcard —
/// so cross-tenant fan-in is impossible by construction, not merely by convention.
/// </summary>
public sealed class BrokerChannelIsolationGuardTests
{
    [Fact]
    public void The_subscribe_port_only_exposes_a_single_application_scoped_subscribe_method()
    {
        var subscribeMethods = typeof(IChangeSubscriber)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == nameof(IChangeSubscriber.SubscribeAsync))
            .ToArray();

        subscribeMethods.Should().ContainSingle("there must be no alternative (e.g. wildcard) subscribe overload");

        var firstParameter = subscribeMethods[0].GetParameters()[0];
        firstParameter.ParameterType.Should().Be(typeof(string));
        firstParameter.Name.Should().Be("applicationName",
            "a subscriber is scoped to one named application, never a pattern");
    }

    [Theory]
    [InlineData("SERVICE-A")]
    [InlineData("SERVICE-B")]
    public void A_channel_is_always_a_literal_application_channel_never_a_wildcard(string applicationName)
    {
        var channel = ConfigurationChangeChannel.ForApplication(applicationName);

        channel.Should().Be($"config-changes:{applicationName}");
        channel.Should().NotContain("*", "a wildcard channel would let one subscriber see every tenant");
    }

    [Fact]
    public void The_change_signal_type_cannot_carry_a_configuration_value()
    {
        // Even a hostile publisher cannot smuggle a value: the contract has no field for one.
        var propertyNames = typeof(ConfigurationChangeSignal)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        propertyNames.Should().BeEquivalentTo(new[]
        {
            nameof(ConfigurationChangeSignal.ApplicationName),
            nameof(ConfigurationChangeSignal.RecordId)
        });
    }
}
