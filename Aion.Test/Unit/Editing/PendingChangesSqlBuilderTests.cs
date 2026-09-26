using Aion.Components.Querying.Editing;
using Aion.Contracts.Database;
using Aion.Contracts.Queries.Editing;
using Aion.Core.Database.MySql;
using Aion.Core.Database.PostgreSQL;
using Aion.Core.Database.SqlServer;
using Aion.Test.TestDoubles;
using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class PendingChangesSqlBuilderTests
{
    public static TheoryData<IStandardDatabaseCommands, DatabaseType, string, string> Engines => new()
    {
        { new PostgreSqlCommands(), DatabaseType.PostgreSQL, "public", "UPDATE \"public\".\"users\"\nSET \"name\" = '', \"note\" = NULL\nWHERE \"id\" = 1;" },
        { new PGliteCommands(), DatabaseType.WasmPostgreSQL, "public", "UPDATE \"users\"\nSET \"name\" = '', \"note\" = NULL\nWHERE \"id\" = 1;" },
        { new SqliteWasmCommands(), DatabaseType.WasmSQLite, "", "UPDATE \"users\"\nSET \"name\" = '', \"note\" = NULL\nWHERE \"id\" = 1;" },
        { new MySqlCommands(), DatabaseType.MySQL, "", "UPDATE `users`\nSET `name` = '', `note` = NULL\nWHERE `id` = 1;" },
        { new SqlServerCommands(), DatabaseType.SQLServer, "dbo", "UPDATE [dbo].[users]\nSET [name] = N'', [note] = NULL\nWHERE [id] = 1;" }
    };

    private static readonly List<ColumnInfo> Columns =
    [
        new() { Name = "id", DataType = "integer", IsPrimaryKey = true },
        new() { Name = "name", DataType = "text" },
        new() { Name = "note", DataType = "text", IsNullable = true }
    ];

    // Commits a cell the way the grid's editor does, from what was typed or set to what is stored.
    private static void Commit(EditState edits, Dictionary<string, object> row, CellEditSession session)
    {
        session.TryGetCommitText(out var text).ShouldBeTrue();
        edits.UpdateCell(0, session.Column, CellEditText.Resolve(text, row[session.Column]), row);
    }

    [Theory]
    [MemberData(nameof(Engines))]
    public async Task ClearedTextAndSetNull_StayDistinctInTheSql(IStandardDatabaseCommands commands, DatabaseType engine, string schema, string expected)
    {
        var fixture = new EditingFixture(commands: commands, engine: engine);
        var row = new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada", ["note"] = "first" };
        var result = new EditableQueryResult
        {
            Columns = ["id", "name", "note"],
            Rows = [row],
            SourceTable = "users",
            SourceSchema = schema,
            SourceDatabase = EditingFixture.DatabaseName,
            ConnectionId = fixture.Connection.Id,
            ColumnMetadata = Columns
        };
        var edits = new EditState();

        var cleared = new CellEditSession(0, "name", "Ada", EditableColumn.For("name", Columns));
        cleared.Type("");
        Commit(edits, row, cleared);

        var nulled = new CellEditSession(0, "note", "first", EditableColumn.For("note", Columns));
        nulled.SetNull().ShouldBeTrue();
        Commit(edits, row, nulled);

        var plan = await fixture.CreateSqlBuilder().BuildAsync(result, edits.PendingChanges);

        plan.Generation.ValidationError.ShouldBeNull();
        plan.Generation.Statements.ShouldHaveSingleItem().Sql.ShouldBe(expected);
    }
}
