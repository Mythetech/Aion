using System.Data.Common;
using Aion.Components.Connections;
using Aion.Contracts.Database;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStringComposerTests
{
    private const string TrickyPassword = "p;a'ss\"w=rd ";

    private static ConnectionDialogModel Model(DatabaseType type, string port) => new()
    {
        Type = type,
        Host = "db.example.com",
        Port = port,
        Database = "app",
        Username = "admin",
        Password = TrickyPassword
    };

    [Fact]
    public void Build_PostgreSql_QuotesPasswordSoNpgsqlReadsItBack()
    {
        var connectionString = ConnectionStringComposer.Build(Model(DatabaseType.PostgreSQL, "5432"));

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        parsed.Host.ShouldBe("db.example.com");
        parsed.Port.ShouldBe(5432);
        parsed.Database.ShouldBe("app");
        parsed.Username.ShouldBe("admin");
        parsed.Password.ShouldBe(TrickyPassword);
    }

    [Fact]
    public void Build_MySql_QuotesPasswordSoMySqlReadsItBack()
    {
        var connectionString = ConnectionStringComposer.Build(Model(DatabaseType.MySQL, "3306"));

        var parsed = new MySqlConnectionStringBuilder(connectionString);
        parsed.Server.ShouldBe("db.example.com");
        parsed.Port.ShouldBe(3306u);
        parsed.Database.ShouldBe("app");
        parsed.UserID.ShouldBe("admin");
        parsed.Password.ShouldBe(TrickyPassword);
    }

    [Fact]
    public void Build_SqlServer_UsesPortWhenInstanceIsEmpty()
    {
        var model = Model(DatabaseType.SQLServer, "1433");
        model.Instance = "";

        var parsed = new SqlConnectionStringBuilder(ConnectionStringComposer.Build(model));

        parsed.DataSource.ShouldBe("db.example.com,1433");
        parsed.InitialCatalog.ShouldBe("app");
        parsed.UserID.ShouldBe("admin");
        parsed.Password.ShouldBe(TrickyPassword);
    }

    [Fact]
    public void Build_SqlServer_UsesNamedInstance()
    {
        var model = Model(DatabaseType.SQLServer, "1433");
        model.Instance = "SQLEXPRESS";

        var parsed = new SqlConnectionStringBuilder(ConnectionStringComposer.Build(model));

        parsed.DataSource.ShouldBe(@"db.example.com\SQLEXPRESS");
    }

    [Fact]
    public void Build_SqlServer_WritesEncryptionOptions()
    {
        var model = Model(DatabaseType.SQLServer, "1433");
        model.Encrypt = false;
        model.TrustServerCertificate = true;

        var parsed = new SqlConnectionStringBuilder(ConnectionStringComposer.Build(model));

        parsed.Encrypt.ShouldBe(SqlConnectionEncryptOption.Optional);
        parsed.TrustServerCertificate.ShouldBeTrue();
    }

    [Fact]
    public void Build_SqlServer_WindowsAuthOmitsCredentials()
    {
        var model = Model(DatabaseType.SQLServer, "1433");
        model.UseWindowsAuth = true;

        var parsed = new SqlConnectionStringBuilder(ConnectionStringComposer.Build(model));

        parsed.IntegratedSecurity.ShouldBeTrue();
        parsed.UserID.ShouldBeEmpty();
        parsed.Password.ShouldBeEmpty();
    }

    [Fact]
    public void Build_LiteDb_QuotesPasswordSoLiteDbReadsItBack()
    {
        var model = new ConnectionDialogModel
        {
            Type = DatabaseType.LiteDB,
            Host = "/tmp/my data/app.db",
            Password = "se;cret"
        };

        var parsed = new LiteDB.ConnectionString(ConnectionStringComposer.Build(model));

        parsed.Filename.ShouldBe("/tmp/my data/app.db");
        parsed.Password.ShouldBe("se;cret");
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSQL, "5432")]
    [InlineData(DatabaseType.MySQL, "3306")]
    [InlineData(DatabaseType.SQLServer, "1433")]
    public void BuildThenPopulate_RoundTripsBasicFields(DatabaseType type, string port)
    {
        var original = Model(type, port);
        original.TrustServerCertificate = true;

        var roundTripped = new ConnectionDialogModel { Type = type };
        ConnectionStringComposer.TryPopulate(roundTripped, ConnectionStringComposer.Build(original)).ShouldBeTrue();

        roundTripped.Host.ShouldBe(original.Host);
        roundTripped.Port.ShouldBe(port);
        roundTripped.Database.ShouldBe("app");
        roundTripped.Username.ShouldBe("admin");
        roundTripped.Password.ShouldBe(TrickyPassword);
        if (type == DatabaseType.SQLServer)
        {
            roundTripped.TrustServerCertificate.ShouldBeTrue();
            roundTripped.Encrypt.ShouldBeTrue();
        }
    }

    [Fact]
    public void TryPopulate_ReadsSynonymsFromHandWrittenStrings()
    {
        var model = new ConnectionDialogModel { Type = DatabaseType.SQLServer };

        ConnectionStringComposer.TryPopulate(model,
            "Data Source=tcp:sql.local,14330;UID=sa;PWD='x;y';Database=Sales;Encrypt=no;Trusted_Connection=no");

        model.Host.ShouldBe("tcp:sql.local");
        model.Port.ShouldBe("14330");
        model.Username.ShouldBe("sa");
        model.Password.ShouldBe("x;y");
        model.Database.ShouldBe("Sales");
        model.Encrypt.ShouldBeFalse();
        model.UseWindowsAuth.ShouldBeFalse();
    }

    [Fact]
    public void TryPopulate_SqlServerWithoutEncrypt_DefaultsToEncrypted()
    {
        var model = new ConnectionDialogModel { Type = DatabaseType.SQLServer };

        ConnectionStringComposer.TryPopulate(model, "Server=localhost;User Id=sa;Password=pw");

        model.Encrypt.ShouldBeTrue();
        model.TrustServerCertificate.ShouldBeFalse();
    }

    [Fact]
    public void TryPopulate_InvalidString_ReturnsFalseAndLeavesModel()
    {
        var model = new ConnectionDialogModel { Type = DatabaseType.PostgreSQL, Host = "keep" };

        ConnectionStringComposer.TryPopulate(model, "this is not a connection string").ShouldBeFalse();

        model.Host.ShouldBe("keep");
    }

    [Fact]
    public void Build_PreservesUnmanagedOptionsAndReplacesSynonyms()
    {
        var model = Model(DatabaseType.PostgreSQL, "6543");

        var connectionString = ConnectionStringComposer.Build(model,
            "Server=old-host;User Id=old-user;Password=old;SSL Mode=Require;Application Name=Aion");

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        parsed.Host.ShouldBe("db.example.com");
        parsed.Username.ShouldBe("admin");
        parsed.Password.ShouldBe(TrickyPassword);
        parsed.SslMode.ShouldBe(SslMode.Require);
        parsed.ApplicationName.ShouldBe("Aion");

        var keys = new DbConnectionStringBuilder { ConnectionString = connectionString }.Keys.Cast<string>().ToList();
        keys.ShouldNotContain(k => k.Equals("server", StringComparison.OrdinalIgnoreCase));
        keys.ShouldNotContain(k => k.Equals("user id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_SqlServer_KeepsStrictEncryptionFromOriginal()
    {
        var model = Model(DatabaseType.SQLServer, "1433");

        var connectionString = ConnectionStringComposer.Build(model, "Server=x;Encrypt=Strict");

        new SqlConnectionStringBuilder(connectionString).Encrypt.ShouldBe(SqlConnectionEncryptOption.Strict);
    }

    [Fact]
    public void WithConnectTimeout_SqlServer_ReplacesExistingTimeout()
    {
        var connectionString = ConnectionStringComposer.WithConnectTimeout(
            "Server=localhost;Connection Timeout=30;User Id=sa", DatabaseType.SQLServer, TimeSpan.FromSeconds(5));

        new SqlConnectionStringBuilder(connectionString).ConnectTimeout.ShouldBe(5);
    }

    [Fact]
    public void WithConnectTimeout_PostgreSql_SetsTimeout()
    {
        var connectionString = ConnectionStringComposer.WithConnectTimeout(
            "Host=localhost;Username=postgres", DatabaseType.PostgreSQL, TimeSpan.FromSeconds(3));

        new NpgsqlConnectionStringBuilder(connectionString).Timeout.ShouldBe(3);
    }

    [Fact]
    public void WithConnectTimeout_MySql_SetsTimeout()
    {
        var connectionString = ConnectionStringComposer.WithConnectTimeout(
            "Server=localhost;Uid=root", DatabaseType.MySQL, TimeSpan.FromSeconds(4));

        new MySqlConnectionStringBuilder(connectionString).ConnectionTimeout.ShouldBe(4u);
    }

    [Fact]
    public void DescribeTarget_ReturnsHostOrFileName()
    {
        ConnectionStringComposer.DescribeTarget(DatabaseType.PostgreSQL, "Host=pg.local;Username=u").ShouldBe("pg.local");
        ConnectionStringComposer.DescribeTarget(DatabaseType.LiteDB, "Filename=/data/app.db").ShouldBe("app.db");
        ConnectionStringComposer.DescribeTarget(DatabaseType.MySQL, "not valid").ShouldBeNull();
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSQL, "Host=pg.local;Database=sales;Username=u", "sales")]
    [InlineData(DatabaseType.MySQL, "Server=localhost;Initial Catalog=shop;Uid=root", "shop")]
    [InlineData(DatabaseType.SQLServer, "Server=sql;Initial Catalog=Reporting;User Id=sa", "Reporting")]
    [InlineData(DatabaseType.PostgreSQL, "Host=pg.local;Username=u", null)]
    [InlineData(DatabaseType.PostgreSQL, "Host=pg.local;Database=;Username=u", null)]
    [InlineData(DatabaseType.LiteDB, "Filename=/data/app.db", null)]
    public void FindDatabase_ReturnsTheDatabaseTheStringConnectsTo(DatabaseType type, string connectionString, string? expected)
    {
        ConnectionStringComposer.FindDatabase(type, connectionString).ShouldBe(expected);
    }
}
