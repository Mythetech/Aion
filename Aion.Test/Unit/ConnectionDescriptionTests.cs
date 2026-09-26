using Aion.Components.Connections;
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
}
