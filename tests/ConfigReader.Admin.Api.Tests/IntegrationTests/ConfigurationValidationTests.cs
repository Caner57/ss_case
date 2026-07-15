using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// Input/type validation behaviour (CFG-5.2): invalid Type/Value combinations, whitelist
/// violations, missing required fields, length and format limits are rejected with 400 before
/// anything reaches the store.
/// </summary>
public sealed class ConfigurationValidationTests
{
    private static async Task<HttpResponseMessage> Post(
        AdminApiFactory factory, string name, string type, string value, string applicationName) =>
        await factory.CreateAuthedClient().PostAsJsonAsync(
            "/api/configurations",
            new CreateConfigurationRequest(name, type, value, true, applicationName));

    [Fact]
    public async Task Bool_type_with_non_boolean_value_is_rejected()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "IsBasketEnabled", "bool", "evet", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.Store.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Int_type_with_non_numeric_value_is_rejected()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "MaxItemCount", "int", "abc", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Int_type_with_numeric_value_is_accepted()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "MaxItemCount", "int", "50", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Integer_variant_token_is_accepted_like_the_library()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "MaxItemCount", "integer", "50", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Whitelist_violating_type_is_rejected()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "Payload", "object", "{}", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Empty_name_is_rejected()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "", "string", "soty.io", "SERVICE-A");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Malformed_application_name_is_rejected()
    {
        using var factory = new AdminApiFactory();

        var response = await Post(factory, "SiteName", "string", "soty.io", "service a!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Oversized_value_is_rejected()
    {
        using var factory = new AdminApiFactory();
        var oversized = new string('x', 1_000_000);

        var response = await Post(factory, "Blob", "string", oversized, "SERVICE-A");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
    }
}
