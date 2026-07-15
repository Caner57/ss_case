using System.Net;
using System.Net.Http.Json;
using ConfigReader.Admin.Api.Application;
using ConfigReader.Admin.Api.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace ConfigReader.Admin.Api.Tests.IntegrationTests;

/// <summary>
/// Verifies the configuration change trail (CFG-9.2): every create/update leaves an audit entry
/// with who, when, which record and old→new value, and an audit-write failure is surfaced in the
/// logs rather than silently swallowed.
/// </summary>
public sealed class AuditLoggingTests
{
    private static CreateConfigurationRequest ValidCreate(
        string name = "SiteName",
        string type = "string",
        string value = "soty.io",
        bool isActive = true,
        string applicationName = "SERVICE-A") =>
        new(name, type, value, isActive, applicationName);

    [Fact]
    public async Task Creating_a_record_writes_a_create_audit_entry_with_actor_and_recent_timestamp()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var response = await client.PostAsJsonAsync("/api/configurations",
            ValidCreate(name: "MaxItemCount", type: "int", value: "50", applicationName: "SERVICE-A"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        factory.AuditLog.Entries.Should().ContainSingle();
        var entry = factory.AuditLog.Entries[0];
        entry.Operation.Should().Be(AuditOperation.Create);
        entry.Actor.Should().Be("admin");
        entry.ApplicationName.Should().Be("SERVICE-A");
        entry.Name.Should().Be("MaxItemCount");
        entry.OldValue.Should().BeNull();
        entry.NewValue.Should().Be("50");
        entry.TimestampUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Updating_a_record_writes_an_update_audit_entry_with_old_and_new_value()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        var created = await (await client.PostAsJsonAsync("/api/configurations",
                ValidCreate(name: "MaxItemCount", type: "int", value: "50")))
            .Content.ReadFromJsonAsync<ConfigurationDto>();

        var update = new UpdateConfigurationRequest("MaxItemCount", "int", "100", true, "SERVICE-A");
        var put = await client.PutAsJsonAsync($"/api/configurations/{created!.Id}", update);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateEntry = factory.AuditLog.Entries.Should().ContainSingle(e => e.Operation == AuditOperation.Update).Subject;
        updateEntry.Actor.Should().Be("admin");
        updateEntry.Name.Should().Be("MaxItemCount");
        updateEntry.OldValue.Should().Be("50");
        updateEntry.NewValue.Should().Be("100");
    }

    [Fact]
    public async Task Audit_entry_never_contains_a_secret_only_config_field_and_value()
    {
        using var factory = new AdminApiFactory();
        var client = factory.CreateAuthedClient();

        await client.PostAsJsonAsync("/api/configurations", ValidCreate());

        var entry = factory.AuditLog.Entries.Should().ContainSingle().Subject;
        var serialized = string.Join('|', entry.Actor, entry.ApplicationName, entry.Name,
            entry.OldValue ?? string.Empty, entry.NewValue);
        serialized.Should().NotContainEquivalentOf("mongodb://");
        serialized.Should().NotContainEquivalentOf("password");
        serialized.Should().NotContainEquivalentOf(AdminApiFactory.ValidApiKey);
    }

    [Fact]
    public async Task When_audit_write_fails_the_change_still_succeeds_but_the_failure_is_logged()
    {
        using var factory = new AdminApiFactory();
        factory.AuditLog.ThrowOnWrite = true;
        var client = factory.CreateAuthedClient();

        var response = await client.PostAsJsonAsync("/api/configurations", ValidCreate());

        // Best-effort: the configuration change is not rolled back by an audit outage...
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // ...but the failure must be surfaced loudly, never silently swallowed.
        factory.Logs.Entries.Should().Contain(log =>
            log.Level == LogLevel.Error && log.Message.Contains("audit", StringComparison.OrdinalIgnoreCase));
    }
}
