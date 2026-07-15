using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// API edge-hardening behaviour (CFG-9.6): restricted CORS, no internal error leakage in
/// production, rate limiting on writes, and baseline security headers.
/// </summary>
public sealed class EdgeHardeningTests
{
    private static CreateConfigurationRequest ValidCreate() =>
        new("SiteName", "string", "soty.io", true, "SERVICE-A");

    [Fact]
    public async Task Allowed_origin_receives_cors_header()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/configurations");
        request.Headers.Add("Origin", AdminApiFactory.AllowedOrigin);
        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values!.Should().Contain(AdminApiFactory.AllowedOrigin);
    }

    [Fact]
    public async Task Unknown_origin_does_not_receive_cors_header()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/configurations");
        request.Headers.Add("Origin", "https://evil.example.com");
        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Production_error_response_does_not_leak_internal_detail()
    {
        using var factory = new AdminApiFactory(environment: "Production");
        factory.Store.ThrowOnRead = true;
        var client = factory.CreateAuthedClient();

        var response = await client.GetAsync("/api/configurations");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("SECRET-INTERNAL-DETAIL");
        body.Should().NotContain("mongodb://");
        body.Should().NotContain("InvalidOperationException");
        body.Should().Contain("correlationId");
    }

    [Fact]
    public async Task Write_endpoint_starts_returning_429_under_burst()
    {
        using var factory = new AdminApiFactory(writePermitLimit: 3);
        var client = factory.CreateAuthedClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            var response = await client.PostAsJsonAsync("/api/configurations", ValidCreate());
            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Responses_carry_baseline_security_headers()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var response = await client.GetAsync("/api/configurations");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }
}
