using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

public sealed class GetValueTypeConversionTests
{
    private const string ApplicationName = "SERVICE-A";

    private static ConfigurationRecord Record(string name, string type, string value) => new()
    {
        Id = name,
        Name = name,
        Type = type,
        Value = value,
        IsActive = true,
        ApplicationName = ApplicationName
    };

    private static ConfigurationReader ReaderWith(params ConfigurationRecord[] records) =>
        new(ApplicationName, new FakeConfigurationStore(records), refreshTimerIntervalInMs: 1000);

    [Fact]
    public void Returns_string_value_verbatim()
    {
        using var reader = ReaderWith(Record("SiteName", "string", "soty.io"));

        reader.GetValue<string>("SiteName").Should().Be("soty.io");
    }

    [Theory]
    [InlineData("int")]
    [InlineData("Int")]
    [InlineData("integer")]
    [InlineData("Integer")]
    public void Resolves_int_variants_case_insensitively(string typeToken)
    {
        using var reader = ReaderWith(Record("MaxItemCount", typeToken, "50"));

        reader.GetValue<int>("MaxItemCount").Should().Be(50);
    }

    [Fact]
    public void Converts_double_with_invariant_culture()
    {
        using var reader = ReaderWith(Record("Ratio", "double", "3.14"));

        reader.GetValue<double>("Ratio").Should().Be(3.14d);
    }

    [Theory]
    [InlineData("bool", "1", true)]
    [InlineData("boolean", "0", false)]
    [InlineData("Bool", "true", true)]
    [InlineData("Boolean", "false", false)]
    public void Resolves_bool_variants_and_numeric_and_textual_values(string typeToken, string raw, bool expected)
    {
        using var reader = ReaderWith(Record("IsBasketEnabled", typeToken, raw));

        reader.GetValue<bool>("IsBasketEnabled").Should().Be(expected);
    }

    [Fact]
    public void Requesting_wrong_target_type_throws_a_defined_mismatch_instead_of_silent_wrong_value()
    {
        using var reader = ReaderWith(Record("IsBasketEnabled", "bool", "1"));

        var act = () => reader.GetValue<int>("IsBasketEnabled");

        act.Should().Throw<ConfigurationTypeMismatchException>();
    }

    [Fact]
    public void Unknown_stored_type_token_throws_rather_than_returning_wrong_data()
    {
        using var reader = ReaderWith(Record("Weird", "guid", "x"));

        var act = () => reader.GetValue<string>("Weird");

        act.Should().Throw<UnknownConfigurationTypeException>();
    }

    [Fact]
    public void Missing_key_throws_defined_not_found()
    {
        using var reader = ReaderWith(Record("SiteName", "string", "soty.io"));

        var act = () => reader.GetValue<string>("Absent");

        act.Should().Throw<ConfigurationKeyNotFoundException>();
    }
}
