using System.Text.Json;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionModelSerializationTests
{
    private static ConnectionModel CreateConnectedModel() => new()
    {
        Name = "Local",
        ConnectionString = "Host=localhost;Username=postgres",
        Type = DatabaseType.PostgreSQL,
        SaveCredentials = true,
        Active = true,
        HealthStatus = ConnectionHealthStatus.Healthy,
        LastActivityTime = DateTime.UtcNow,
        LastHealthCheckTime = DateTime.UtcNow,
        LastError = "old error",
        Databases = [new DatabaseModel { Name = "app" }]
    };

    [Fact]
    public void Serialize_OmitsRuntimeState()
    {
        var json = JsonSerializer.Serialize(CreateConnectedModel());

        json.ShouldNotContain(nameof(ConnectionModel.Active));
        json.ShouldNotContain(nameof(ConnectionModel.HealthStatus));
        json.ShouldNotContain(nameof(ConnectionModel.LastActivityTime));
        json.ShouldNotContain(nameof(ConnectionModel.LastHealthCheckTime));
        json.ShouldNotContain(nameof(ConnectionModel.LastError));
        json.ShouldNotContain(nameof(ConnectionModel.Databases));
    }

    [Fact]
    public void Serialize_KeepsProfileFields()
    {
        var model = CreateConnectedModel();

        var roundTripped = JsonSerializer.Deserialize<ConnectionModel>(JsonSerializer.Serialize(model))!;

        roundTripped.Id.ShouldBe(model.Id);
        roundTripped.Name.ShouldBe("Local");
        roundTripped.ConnectionString.ShouldBe(model.ConnectionString);
        roundTripped.Type.ShouldBe(DatabaseType.PostgreSQL);
        roundTripped.SaveCredentials.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_ProfileSavedByOlderVersion_IgnoresStaleRuntimeState()
    {
        const string legacyJson = """
            [{"Id":"6f1d2c3b-0000-0000-0000-000000000001","Name":"Old","ConnectionString":"Host=db","Type":0,
              "Databases":[{"Name":"app"}],"Active":true,"SaveCredentials":true,"IsSavedConnection":true,
              "LastActivityTime":"2024-01-01T00:00:00Z","LastHealthCheckTime":"2024-01-01T00:00:00Z","HealthStatus":2}]
            """;

        var connection = JsonSerializer.Deserialize<List<ConnectionModel>>(legacyJson)!.Single();

        connection.Name.ShouldBe("Old");
        connection.Active.ShouldBeFalse();
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.Unknown);
        connection.LastActivityTime.ShouldBeNull();
        connection.Databases.ShouldBeEmpty();
    }
}
