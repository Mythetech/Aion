using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionDescriptionTests
{
    [Theory]
    [InlineData(DatabaseType.WasmSQLite, "SQLite · in-browser")]
    [InlineData(DatabaseType.WasmPostgreSQL, "PGlite · in-browser")]
    [InlineData(DatabaseType.PostgreSQL, "PostgreSQL")]
    [InlineData(DatabaseType.MySQL, "MySQL")]
    [InlineData(DatabaseType.SQLServer, "SQL Server")]
    [InlineData(DatabaseType.SQLite, "SQLite")]
    [InlineData(DatabaseType.LiteDB, "LiteDB")]
    public void EngineLabel_NamesEveryEngineForThePanelHeader(DatabaseType type, string expected)
    {
        ConnectionDescription.EngineLabel(type).ShouldBe(expected);
    }

    [Theory]
    [InlineData(DatabaseType.WasmSQLite, "SQLite")]
    [InlineData(DatabaseType.WasmPostgreSQL, "PGlite")]
    [InlineData(DatabaseType.PostgreSQL, "PostgreSQL")]
    [InlineData(DatabaseType.MySQL, "MySQL")]
    [InlineData(DatabaseType.SQLServer, "SQL Server")]
    [InlineData(DatabaseType.SQLite, "SQLite")]
    [InlineData(DatabaseType.LiteDB, "LiteDB")]
    public void EngineBadge_NamesEveryEngineInAWordOrTwo(DatabaseType type, string expected)
    {
        ConnectionDescription.EngineBadge(type).ShouldBe(expected);
    }

    [Theory]
    [InlineData(DatabaseType.WasmSQLite, "Data Source=analytics")]
    [InlineData(DatabaseType.WasmPostgreSQL, "Host=analytics;Database=analytics")]
    public void Describe_InBrowserEngines_NameNoHost(DatabaseType type, string connectionString)
    {
        var connection = new ConnectionModel { Name = "analytics", Type = type, ConnectionString = connectionString };

        ConnectionDescription.Describe(connection).ShouldBe(ConnectionDescription.EngineName(type));
    }

    [Fact]
    public void Describe_ServerEngines_NameTheirHost()
    {
        var connection = new ConnectionModel { Name = "prod", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=db.internal;Database=orders" };

        ConnectionDescription.Describe(connection).ShouldBe("PostgreSQL on db.internal");
    }
}
