using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// End-to-end CRUD behaviour of the management API (CFG-5.1) hosted via
/// <see cref="AdminApiFactory"/> against the in-memory store.
/// </summary>
public sealed class ConfigurationsCrudTests
{
    private static CreateConfigurationRequest ValidCreate(
        string name = "SiteName",
        string type = "string",
        string value = "soty.io",
        bool isActive = true,
        string applicationName = "SERVICE-A") =>
        new(name, type, value, isActive, applicationName);

    [Fact]
    public async Task Posted_record_is_persisted_and_appears_in_the_list()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var create = await client.PostAsJsonAsync("/api/configurations", ValidCreate());
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<ConfigurationDto>();
        created!.Id.Should().NotBeNullOrWhiteSpace();

        var list = await client.GetFromJsonAsync<List<ConfigurationDto>>("/api/configurations");
        list.Should().ContainSingle(dto => dto.Name == "SiteName" && dto.Value == "soty.io");
    }

    [Fact]
    public async Task Put_updates_the_value_and_the_new_value_is_read_back()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var created = await (await client.PostAsJsonAsync("/api/configurations", ValidCreate()))
            .Content.ReadFromJsonAsync<ConfigurationDto>();

        var update = new UpdateConfigurationRequest("SiteName", "string", "updated.io", true, "SERVICE-A");
        var put = await client.PutAsJsonAsync($"/api/configurations/{created!.Id}", update);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var reloaded = await client.GetFromJsonAsync<ConfigurationDto>($"/api/configurations/{created.Id}");
        reloaded!.Value.Should().Be("updated.io");
    }

    [Fact]
    public async Task List_returns_records_of_every_application_for_management()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        await client.PostAsJsonAsync("/api/configurations", ValidCreate(applicationName: "SERVICE-A"));
        await client.PostAsJsonAsync("/api/configurations",
            ValidCreate(name: "IsBasketEnabled", type: "bool", value: "true", applicationName: "SERVICE-B"));

        var list = await client.GetFromJsonAsync<List<ConfigurationDto>>("/api/configurations");

        list.Should().Contain(dto => dto.ApplicationName == "SERVICE-A");
        list.Should().Contain(dto => dto.ApplicationName == "SERVICE-B");
    }

    [Fact]
    public async Task Get_by_unknown_id_returns_404()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var response = await client.GetAsync("/api/configurations/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
