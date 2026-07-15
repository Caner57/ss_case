using ConfigReader.Core.Domain;
using FluentAssertions;
using Xunit;

namespace ConfigReader.Core.Tests.Domain;

public class ConfigurationTypeResolverTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("Int")]
    [InlineData("INT")]
    [InlineData("integer")]
    [InlineData("Integer")]
    public void Resolve_IntVariants_MapToInt(string token)
    {
        ConfigurationTypeResolver.Resolve(token).Should().Be(ConfigurationType.Int);
    }

    [Theory]
    [InlineData("bool")]
    [InlineData("Bool")]
    [InlineData("boolean")]
    [InlineData("Boolean")]
    [InlineData("BOOLEAN")]
    public void Resolve_BoolVariants_MapToBool(string token)
    {
        ConfigurationTypeResolver.Resolve(token).Should().Be(ConfigurationType.Bool);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("String")]
    [InlineData("STRING")]
    public void Resolve_StringVariants_MapToString(string token)
    {
        ConfigurationTypeResolver.Resolve(token).Should().Be(ConfigurationType.String);
    }

    [Theory]
    [InlineData("double")]
    [InlineData("Double")]
    [InlineData("DOUBLE")]
    public void Resolve_DoubleVariants_MapToDouble(string token)
    {
        ConfigurationTypeResolver.Resolve(token).Should().Be(ConfigurationType.Double);
    }

    [Theory]
    [InlineData("guid")]
    [InlineData("decimal")]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_UnknownOrEmptyToken_ThrowsMeaningfulException(string token)
    {
        var act = () => ConfigurationTypeResolver.Resolve(token);

        act.Should().Throw<UnknownConfigurationTypeException>()
            .Which.Token.Should().Be(token);
    }

    [Fact]
    public void Resolve_NullToken_ThrowsMeaningfulException()
    {
        var act = () => ConfigurationTypeResolver.Resolve(null!);

        act.Should().Throw<UnknownConfigurationTypeException>();
    }
}
