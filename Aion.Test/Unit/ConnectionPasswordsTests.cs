using Aion.Components.Connections;
using Aion.Contracts.Database;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionPasswordsTests
{
    [Theory]
    [InlineData(DatabaseType.PostgreSQL, "Host=db;Username=app;Password=secret")]
    [InlineData(DatabaseType.PostgreSQL, "Host=db;Username=app;PWD=secret")]
    [InlineData(DatabaseType.PostgreSQL, "Host=db;Username=app;PSW=secret")]
    [InlineData(DatabaseType.PostgreSQL, "host=db;username=app;password=secret")]
    [InlineData(DatabaseType.MySQL, "Server=db;Uid=app;Pwd=secret")]
    [InlineData(DatabaseType.MySQL, "Server=db;User Id=app;Password=secret")]
    [InlineData(DatabaseType.SQLServer, "Server=db;User Id=sa;Password=secret")]
    [InlineData(DatabaseType.SQLServer, "Server=db;User Id=sa;PWD=secret")]
    [InlineData(DatabaseType.LiteDB, "Filename=/data/app.db;Password=secret")]
    public void GetPassword_FindsEverySpellingOfTheProvidersPasswordKey(DatabaseType type, string connectionString)
    {
        ConnectionPasswords.GetPassword(type, connectionString).ShouldBe("secret");
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSQL, "Host=db;Username=app")]
    [InlineData(DatabaseType.PostgreSQL, "Host=db;Username=app;Password=")]
    [InlineData(DatabaseType.SQLServer, "Server=db;Integrated Security=True")]
    [InlineData(DatabaseType.LiteDB, "Filename=/data/app.db")]
    [InlineData(DatabaseType.WasmSQLite, "Data Source=sample.db")]
    [InlineData(DatabaseType.PostgreSQL, "")]
    [InlineData(DatabaseType.PostgreSQL, "not a connection string")]
    public void GetPassword_WithoutAPassword_IsNull(DatabaseType type, string connectionString)
    {
        ConnectionPasswords.GetPassword(type, connectionString).ShouldBeNull();
    }

    [Fact]
    public void GetPassword_QuotedValue_KeepsSemicolonsAndEquals()
    {
        var connectionString = new NpgsqlConnectionStringBuilder { Host = "db", Username = "app", Password = "a;b=c'd\"e" }
            .ConnectionString;

        ConnectionPasswords.GetPassword(DatabaseType.PostgreSQL, connectionString).ShouldBe("a;b=c'd\"e");
    }

    [Fact]
    public void WithoutPassword_Postgres_KeepsEverythingElse()
    {
        var stripped = ConnectionPasswords.WithoutPassword(DatabaseType.PostgreSQL,
            "Host=db;Port=6543;Database=app;Username=reader;Password=secret;SSL Mode=Require");

        var parsed = new NpgsqlConnectionStringBuilder(stripped);
        parsed.Password.ShouldBeNull();
        parsed.Host.ShouldBe("db");
        parsed.Port.ShouldBe(6543);
        parsed.Database.ShouldBe("app");
        parsed.Username.ShouldBe("reader");
        parsed.SslMode.ShouldBe(SslMode.Require);
    }

    [Fact]
    public void WithoutPassword_QuotedValue_RemovesAllOfIt()
    {
        var connectionString = new MySqlConnectionStringBuilder { Server = "db", UserID = "app", Password = "x;Server=evil;y=z" }
            .ConnectionString;

        var stripped = ConnectionPasswords.WithoutPassword(DatabaseType.MySQL, connectionString);

        stripped.ShouldNotContain("evil");
        var parsed = new MySqlConnectionStringBuilder(stripped);
        parsed.Password.ShouldBeEmpty();
        parsed.Server.ShouldBe("db");
        parsed.UserID.ShouldBe("app");
    }

    [Fact]
    public void WithoutPassword_RemovesEverySynonymPresent()
    {
        var stripped = ConnectionPasswords.WithoutPassword(DatabaseType.PostgreSQL, "Host=db;Password=one;PWD=two;PSW=three");

        stripped.ShouldNotContain("one");
        stripped.ShouldNotContain("two");
        stripped.ShouldNotContain("three");
        ConnectionPasswords.GetPassword(DatabaseType.PostgreSQL, stripped).ShouldBeNull();
    }

    [Fact]
    public void WithoutPassword_SqlServer_KeepsTheLoginAndOptions()
    {
        var stripped = ConnectionPasswords.WithoutPassword(DatabaseType.SQLServer,
            "Server=db,1433;Initial Catalog=app;User Id=sa;Password=Str0ng!;Encrypt=True;TrustServerCertificate=True");

        var parsed = new SqlConnectionStringBuilder(stripped);
        parsed.Password.ShouldBeEmpty();
        parsed.DataSource.ShouldBe("db,1433");
        parsed.UserID.ShouldBe("sa");
        parsed.TrustServerCertificate.ShouldBeTrue();
    }

    [Fact]
    public void WithoutPassword_LiteDb_DropsTheEncryptionPassword()
    {
        var stripped = ConnectionPasswords.WithoutPassword(DatabaseType.LiteDB, "Filename=/data/app.db;Password=secret;Connection=shared");

        stripped.ShouldNotContain("secret");
        stripped.ShouldContain("/data/app.db");
        stripped.ShouldContain("shared");
    }

    [Fact]
    public void WithoutPassword_NoPassword_ReturnsTheStringAsItWas()
    {
        const string connectionString = "Host=db;Username=app;SSL Mode=Require";

        ConnectionPasswords.WithoutPassword(DatabaseType.PostgreSQL, connectionString).ShouldBe(connectionString);
    }

    [Fact]
    public void WithPassword_AddsAPasswordThatSurvivesQuoting()
    {
        var withPassword = ConnectionPasswords.WithPassword(DatabaseType.PostgreSQL, "Host=db;Username=app", "pa;ss=wo'rd\"");

        new NpgsqlConnectionStringBuilder(withPassword).Password.ShouldBe("pa;ss=wo'rd\"");
    }

    [Fact]
    public void WithPassword_ReplacesAPasswordUnderAnotherSpelling()
    {
        var withPassword = ConnectionPasswords.WithPassword(DatabaseType.SQLServer, "Server=db;User Id=sa;PWD=old", "new");

        withPassword.ShouldNotContain("old");
        new SqlConnectionStringBuilder(withPassword).Password.ShouldBe("new");
    }

    [Fact]
    public void WithPassword_RoundTripsWithWithoutPassword()
    {
        const string original = "Server=db;Port=3307;Database=shop;Uid=app;Pwd=secret";

        var restored = ConnectionPasswords.WithPassword(DatabaseType.MySQL,
            ConnectionPasswords.WithoutPassword(DatabaseType.MySQL, original), "secret");

        var parsed = new MySqlConnectionStringBuilder(restored);
        parsed.Password.ShouldBe("secret");
        parsed.Port.ShouldBe(3307u);
        parsed.Database.ShouldBe("shop");
    }
}
