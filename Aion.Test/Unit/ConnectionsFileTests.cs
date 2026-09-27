using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Npgsql;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionsFileTests
{
    private const string Password = "s3cr;et=\"pa'ss\"";

    public static TheoryData<DatabaseType, string> ConnectionsWithPasswords => new()
    {
        { DatabaseType.PostgreSQL, new NpgsqlConnectionStringBuilder { Host = "db", Username = "app", Password = Password }.ConnectionString },
        { DatabaseType.PostgreSQL, "Host=db;Username=app;PWD=s3cr-plain" },
        { DatabaseType.MySQL, "Server=db;Uid=app;Pwd=s3cr-plain" },
        { DatabaseType.SQLServer, "Server=db;User Id=sa;Password=s3cr-plain;TrustServerCertificate=True" },
        { DatabaseType.LiteDB, "Filename=/data/app.db;Password=s3cr-plain" }
    };

    // Every password starts with "s3cr", which survives any quoting or JSON escaping of the rest of it.
    [Theory]
    [MemberData(nameof(ConnectionsWithPasswords))]
    public void Serialize_NeverWritesThePassword(DatabaseType type, string connectionString)
    {
        var connection = new ConnectionModel { Name = "Prod", Type = type, ConnectionString = connectionString };

        var json = ConnectionsFile.Serialize([connection]);

        json.ShouldNotContain("s3cr");
    }

    [Theory]
    [MemberData(nameof(ConnectionsWithPasswords))]
    public void Serialize_RecordsThatTheConnectionUsesAPassword(DatabaseType type, string connectionString)
    {
        var connection = new ConnectionModel { Name = "Prod", Type = type, ConnectionString = connectionString };

        var loaded = ConnectionsFile.Deserialize(ConnectionsFile.Serialize([connection])).Connections.Single();

        loaded.UsesPassword.ShouldBeTrue();
        ConnectionPasswords.GetPassword(type, loaded.ConnectionString).ShouldBeNull();
    }

    [Fact]
    public void Serialize_LeavesTheRuntimeConnectionStringAlone()
    {
        const string connectionString = "Host=db;Username=app;Password=secret";
        var connection = new ConnectionModel { Name = "Prod", Type = DatabaseType.PostgreSQL, ConnectionString = connectionString };

        ConnectionsFile.Serialize([connection]);

        connection.ConnectionString.ShouldBe(connectionString);
    }

    [Fact]
    public void RoundTrip_KeepsTheProfileAndWhereThePasswordIs()
    {
        var connection = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db;Username=app;Password=secret;SSL Mode=Require",
            PasswordStore = "macOS Keychain"
        };

        var contents = ConnectionsFile.Deserialize(ConnectionsFile.Serialize([connection]));

        contents.WipedPasswords.ShouldBeFalse();
        var loaded = contents.Connections.Single();
        loaded.Id.ShouldBe(connection.Id);
        loaded.Name.ShouldBe("Prod");
        loaded.Type.ShouldBe(DatabaseType.PostgreSQL);
        loaded.PasswordStore.ShouldBe("macOS Keychain");
        loaded.IsSavedConnection.ShouldBeTrue();
        new NpgsqlConnectionStringBuilder(loaded.ConnectionString).SslMode.ShouldBe(SslMode.Require);
    }

    [Fact]
    public void RoundTrip_ConnectionWithoutAPassword_DoesNotUseOne()
    {
        var connection = new ConnectionModel
        {
            Name = "Local",
            Type = DatabaseType.SQLServer,
            ConnectionString = "Server=localhost;Integrated Security=True"
        };

        var loaded = ConnectionsFile.Deserialize(ConnectionsFile.Serialize([connection])).Connections.Single();

        loaded.UsesPassword.ShouldBeFalse();
        loaded.PasswordStore.ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_ConnectionStillWaitingForItsPassword_KeepsUsingOne()
    {
        var connection = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db;Username=app",
            UsesPassword = true
        };

        var loaded = ConnectionsFile.Deserialize(ConnectionsFile.Serialize([connection])).Connections.Single();

        loaded.UsesPassword.ShouldBeTrue();
    }

    [Fact]
    public void Serialize_OmitsRuntimeState()
    {
        var connection = new ConnectionModel
        {
            Name = "Prod",
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db",
            Active = true,
            HealthStatus = ConnectionHealthStatus.Healthy,
            LastError = "old error",
            Databases = [new DatabaseModel { Name = "app" }]
        };

        var json = ConnectionsFile.Serialize([connection]);

        json.ShouldNotContain("Active");
        json.ShouldNotContain("HealthStatus");
        json.ShouldNotContain("old error");
        json.ShouldNotContain("Databases");
    }

    [Fact]
    public void Deserialize_FileWrittenBeforePasswordsMovedOut_StripsAndFlagsThePassword()
    {
        const string legacyJson = """
            [{"Id":"6f1d2c3b-0000-0000-0000-000000000001","Name":"Old","ConnectionString":"Host=db;Username=app;Password=hunter2",
              "Type":0,"SaveCredentials":true,"IsSavedConnection":true},
             {"Id":"6f1d2c3b-0000-0000-0000-000000000002","Name":"Trust","ConnectionString":"Host=local;Username=app",
              "Type":0,"SaveCredentials":true,"IsSavedConnection":true}]
            """;

        var contents = ConnectionsFile.Deserialize(legacyJson);

        contents.WipedPasswords.ShouldBeTrue();
        var old = contents.Connections.Single(c => c.Name == "Old");
        old.ConnectionString.ShouldNotContain("hunter2");
        old.UsesPassword.ShouldBeTrue();
        old.PasswordStore.ShouldBeNull();
        var trust = contents.Connections.Single(c => c.Name == "Trust");
        trust.UsesPassword.ShouldBeFalse();
        ConnectionsFile.Serialize(contents.Connections).ShouldNotContain("hunter2");
    }

    [Fact]
    public void Deserialize_EmptyFile_HasNoConnections()
    {
        ConnectionsFile.Deserialize("[]").Connections.ShouldBeEmpty();
    }
}
