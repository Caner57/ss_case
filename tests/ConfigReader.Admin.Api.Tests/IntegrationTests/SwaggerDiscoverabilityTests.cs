using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// Verifies the OpenAPI/Swagger document (CFG-5.3) is served and describes every CRUD endpoint
/// and the DTO schema, so the API stays self-documenting and try-it-out works.
/// </summary>
public sealed class SwaggerDiscoverabilityTests
{
    [Fact]
    public async Task Swagger_document_describes_all_crud_endpoints_and_the_dto_schema()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var collection = paths.GetProperty("/api/configurations");
        collection.TryGetProperty("get", out _).Should().BeTrue();
        collection.TryGetProperty("post", out _).Should().BeTrue();

        var item = paths.GetProperty("/api/configurations/{id}");
        item.TryGetProperty("get", out _).Should().BeTrue();
        item.TryGetProperty("put", out _).Should().BeTrue();

        var dtoProperties = document.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("ConfigurationDto").GetProperty("properties");

        foreach (var field in new[] { "id", "name", "type", "value", "isActive", "applicationName" })
        {
            dtoProperties.TryGetProperty(field, out _).Should().BeTrue($"schema should expose '{field}'");
        }

        collection.GetProperty("post").GetProperty("responses")
            .TryGetProperty("400", out _).Should().BeTrue();
    }
}
