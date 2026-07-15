using ConfigReader.Core.Application.Messaging;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application.Messaging;

public sealed class ConfigurationChangeChannelTests
{
    [Fact]
    public void ForApplication_prefixes_the_application_name_with_the_shared_channel_prefix()
    {
        ConfigurationChangeChannel.ForApplication("SERVICE-A")
            .Should().Be("config-changes:SERVICE-A");
    }

    [Fact]
    public void ForApplication_gives_each_application_its_own_distinct_channel()
    {
        var serviceA = ConfigurationChangeChannel.ForApplication("SERVICE-A");
        var serviceB = ConfigurationChangeChannel.ForApplication("SERVICE-B");

        serviceA.Should().NotBe(serviceB);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForApplication_rejects_a_missing_application_name(string? applicationName)
    {
        var act = () => ConfigurationChangeChannel.ForApplication(applicationName!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Signal_carries_only_the_application_name_and_record_id_never_a_value()
    {
        // CFG-9.3: the broker contract must not be able to transport a configuration value.
        var properties = typeof(ConfigurationChangeSignal)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        properties.Should().BeEquivalentTo(new[] { nameof(ConfigurationChangeSignal.ApplicationName), nameof(ConfigurationChangeSignal.RecordId) });
        properties.Should().NotContain("Value");
    }
}
