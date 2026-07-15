using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// API-key authentication behaviour (CFG-9.1): every endpoint — reads included — is closed to
/// anonymous or invalid callers, while a valid key is processed normally.
/// </summary>
public sealed class AuthenticationTests
{
    private static CreateConfigurationRequest ValidCreate() =>
        new("SiteName", "string", "soty.io", true, "SERVICE-A");

    [Fact]
    public async Task Post_without_api_key_is_rejected_with_401_and_nothing_is_written()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/configurations", ValidCreate());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.Store.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Put_with_invalid_key_is_rejected_with_401()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AdminApiFactory.ApiKeyHeader, "wrong-key");

        var response = await client.PutAsJsonAsync(
            "/api/configurations/anything",
            new UpdateConfigurationRequest("SiteName", "string", "x", true, "SERVICE-A"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_without_api_key_is_rejected_with_401()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/configurations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_key_is_accepted()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var create = await client.PostAsJsonAsync("/api/configurations", ValidCreate());
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetAsync("/api/configurations");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
