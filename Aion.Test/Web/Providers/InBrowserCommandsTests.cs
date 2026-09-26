using Aion.Contracts.Database;
using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Web.Providers;

public class InBrowserCommandsTests
{
    private static readonly ColumnDefinition[] Columns =
    [
        new("id", "integer", false, IsPrimaryKey: true),
        new("say \"hi\"", "text", true, "'hello'")
    ];

    [Fact]
    public async Task Sqlite_CreateTable_EscapesQuotesInNames()
    {
        var sql = await new SqliteWasmCommands().GenerateCreateTableScript("shop", "", "my \"table\"", Columns);

        sql.ShouldBe("CREATE TABLE \"my \"\"table\"\"\" (\n    \"id\" integer PRIMARY KEY,\n    \"say \"\"hi\"\"\" text DEFAULT 'hello'\n);");
    }

    [Fact]
    public async Task PGlite_CreateTable_EscapesQuotesInNames()
    {
        var sql = await new PGliteCommands().GenerateCreateTableScript("shop", "", "my \"table\"", Columns);

        sql.ShouldBe("CREATE TABLE \"my \"\"table\"\"\" (\n    \"id\" integer PRIMARY KEY,\n    \"say \"\"hi\"\"\" text DEFAULT 'hello'\n);");
    }
}
