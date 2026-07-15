using ConfigReader.Core.Application;
using ConfigReader.Storage.Mongo;
using FluentAssertions;

namespace ConfigReader.Storage.Mongo.Tests.UnitTests;

public sealed class MongoConfigurationStoreFactoryTests
{
    private const string ConnectionStringWithDatabase =
        "mongodb://user:pass@localhost:27017/configdb?authSource=configdb";

    [Fact]
    public void Create_returns_a_configuration_store_for_a_valid_connection_string()
    {
        var factory = new MongoConfigurationStoreFactory();

        var store = factory.Create(ConnectionStringWithDatabase);

        store.Should().NotBeNull();
        store.Should().BeAssignableTo<IConfigurationStore>();
    }

    [Fact]
    public void Create_builds_a_store_when_the_connection_string_omits_a_database_name()
    {
        var factory = new MongoConfigurationStoreFactory();

        var store = factory.Create("mongodb://localhost:27017");

        store.Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_missing_connection_string(string? connectionString)
    {
        var factory = new MongoConfigurationStoreFactory();

        var act = () => factory.Create(connectionString!);

        act.Should().Throw<ArgumentException>();
    }
}
