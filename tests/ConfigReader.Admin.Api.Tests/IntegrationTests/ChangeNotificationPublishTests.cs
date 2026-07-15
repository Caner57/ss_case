using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// Verifies the Admin API publish integration (CFG-4.2): a create/update emits a value-free change
/// signal on the record's own application channel, isolation is preserved, and a broker outage
/// never fails the write.
/// </summary>
public sealed class ChangeNotificationPublishTests
{
    private static CreateConfigurationRequest ValidCreate(
        string name = "SiteName",
        string type = "string",
        string value = "soty.io",
        bool isActive = true,
        string applicationName = "SERVICE-A") =>
        new(name, type, value, isActive, applicationName);

    [Fact]
    public async Task Creating_a_record_publishes_a_signal_for_that_application_with_the_new_id()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var created = await (await client.PostAsJsonAsync("/api/configurations",
                ValidCreate(applicationName: "SERVICE-A")))
            .Content.ReadFromJsonAsync<ConfigurationDto>();

        var signal = factory.ChangeNotifier.Published.Should().ContainSingle().Subject;
        signal.ApplicationName.Should().Be("SERVICE-A");
        signal.RecordId.Should().Be(created!.Id);
    }

    [Fact]
    public async Task Updating_a_record_publishes_a_signal_for_that_application_with_the_record_id()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var created = await (await client.PostAsJsonAsync("/api/configurations",
                ValidCreate(name: "MaxItemCount", type: "int", value: "50")))
            .Content.ReadFromJsonAsync<ConfigurationDto>();
        factory.ChangeNotifier.Published.Should().ContainSingle();

        var update = new UpdateConfigurationRequest("MaxItemCount", "int", "100", true, "SERVICE-A");
        var put = await client.PutAsJsonAsync($"/api/configurations/{created!.Id}", update);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateSignal = factory.ChangeNotifier.Published.Last();
        updateSignal.ApplicationName.Should().Be("SERVICE-A");
        updateSignal.RecordId.Should().Be(created.Id);
    }

    [Fact]
    public async Task Published_signal_never_carries_the_configuration_value()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        await client.PostAsJsonAsync("/api/configurations",
            ValidCreate(name: "SiteName", value: "soty.io"));

        // CFG-9.3: the signal only names the application and record; the value stays out of the wire.
        var signal = factory.ChangeNotifier.Published.Should().ContainSingle().Subject;
        signal.ApplicationName.Should().NotContain("soty.io");
        signal.RecordId.Should().NotContain("soty.io");
    }

    [Fact]
    public async Task A_service_a_change_is_only_published_on_the_service_a_channel()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        await client.PostAsJsonAsync("/api/configurations", ValidCreate(applicationName: "SERVICE-A"));

        // Isolation: no signal should ever be published naming a different application.
        factory.ChangeNotifier.Published.Should().OnlyContain(s => s.ApplicationName == "SERVICE-A");
    }

    [Fact]
    public async Task When_the_broker_is_unavailable_the_write_still_succeeds_and_the_failure_is_logged()
    {
        using var factory = new AdminApiFactory();
        factory.ChangeNotifier.ThrowOnPublish = true;
        var client = factory.CreateAuthedClient();

        var response = await client.PostAsJsonAsync("/api/configurations", ValidCreate());

        // Best-effort: a broker outage never rolls back or fails the persisted change.
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // ...but the publish failure is surfaced in the logs, not silently swallowed.
        factory.Logs.Entries.Should().Contain(log =>
            log.Level == LogLevel.Error
            && log.Message.Contains("publish", StringComparison.OrdinalIgnoreCase));
    }
}
