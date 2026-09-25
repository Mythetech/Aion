using Aion.Contracts.Database;
using Aion.Core.Database.LiteDB;
using Aion.Core.Database.MySql;
using Aion.Core.Database.PostgreSQL;
using Aion.Core.Database.SqlServer;
using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class EditScriptGenerationTests
{
    private static readonly ColumnValue[] NewName = [new("name", "O'Brien's Hub")];
    private static readonly ColumnValue[] KeyFive = [new("id", 5)];

    public static TheoryData<IStandardDatabaseCommands, string, string> UpdateScripts => new()
    {
        { new PostgreSqlCommands(), "public", "UPDATE \"public\".\"users\"\nSET \"name\" = 'O''Brien''s Hub'\nWHERE \"id\" = 5;" },
        { new PGliteCommands(), "public", "UPDATE \"users\"\nSET \"name\" = 'O''Brien''s Hub'\nWHERE \"id\" = 5;" },
        { new SqliteWasmCommands(), "", "UPDATE \"users\"\nSET \"name\" = 'O''Brien''s Hub'\nWHERE \"id\" = 5;" },
        { new MySqlCommands(), "", "UPDATE `users`\nSET `name` = 'O''Brien''s Hub'\nWHERE `id` = 5;" },
        { new SqlServerCommands(), "dbo", "UPDATE [dbo].[users]\nSET [name] = N'O''Brien''s Hub'\nWHERE [id] = 5;" },
    };

    [Theory]
    [MemberData(nameof(UpdateScripts))]
    public async Task GenerateUpdateScript_QuotesForTheEngineAndTargetsTheKey(IStandardDatabaseCommands commands, string schema, string expected)
    {
        var sql = await commands.GenerateUpdateScript("db", schema, "users", NewName, KeyFive);

        sql.ShouldBe(expected);
    }

    public static TheoryData<IStandardDatabaseCommands, string, string> DeleteScripts => new()
    {
        { new PostgreSqlCommands(), "public", "DELETE FROM \"public\".\"users\"\nWHERE \"id\" = 5;" },
        { new PGliteCommands(), "public", "DELETE FROM \"users\"\nWHERE \"id\" = 5;" },
        { new SqliteWasmCommands(), "", "DELETE FROM \"users\"\nWHERE \"id\" = 5;" },
        { new MySqlCommands(), "", "DELETE FROM `users`\nWHERE `id` = 5;" },
        { new SqlServerCommands(), "dbo", "DELETE FROM [dbo].[users]\nWHERE [id] = 5;" },
    };

    [Theory]
    [MemberData(nameof(DeleteScripts))]
    public async Task GenerateDeleteScript_TargetsTheKey(IStandardDatabaseCommands commands, string schema, string expected)
    {
        var sql = await commands.GenerateDeleteScript("db", schema, "users", KeyFive);

        sql.ShouldBe(expected);
    }

    public static TheoryData<IStandardDatabaseCommands> SqlCommands => new()
    {
        new PostgreSqlCommands(),
        new PGliteCommands(),
        new SqliteWasmCommands(),
        new MySqlCommands(),
        new SqlServerCommands()
    };

    [Theory]
    [MemberData(nameof(SqlCommands))]
    public async Task GenerateUpdateScript_WithoutKeys_Throws(IStandardDatabaseCommands commands)
    {
        await Should.ThrowAsync<ArgumentException>(async () => await commands.GenerateUpdateScript("db", "", "users", NewName, []));
    }

    [Theory]
    [MemberData(nameof(SqlCommands))]
    public async Task GenerateDeleteScript_WithoutKeys_Throws(IStandardDatabaseCommands commands)
    {
        await Should.ThrowAsync<ArgumentException>(async () => await commands.GenerateDeleteScript("db", "", "users", []));
    }

    [Theory]
    [MemberData(nameof(SqlCommands))]
    public async Task GenerateInsertScript_EscapesInjectedValues(IStandardDatabaseCommands commands)
    {
        var sql = await commands.GenerateInsertScript("db", "", "users",
        [
            new ColumnValue("id", 7),
            new ColumnValue("name", "'); DROP TABLE users; --")
        ]);

        sql.ShouldContain("'''); DROP TABLE users; --'");
        sql.ShouldEndWith(");");
    }

    [Fact]
    public async Task GenerateUpdateScript_CompositeKeyWithApostrophe()
    {
        var sql = await new MySqlCommands().GenerateUpdateScript("db", "", "memberships",
            [new ColumnValue("role", "admin")],
            [new ColumnValue("team", "O'Brien"), new ColumnValue("user_id", 0)]);

        sql.ShouldBe("UPDATE `memberships`\nSET `role` = 'admin'\nWHERE `team` = 'O''Brien' AND `user_id` = 0;");
    }

    [Fact]
    public async Task LiteDb_RefusesKeyedUpdatesAndDeletes()
    {
        var commands = new LiteDBCommands();

        await Should.ThrowAsync<NotSupportedException>(async () => await commands.GenerateUpdateScript("db", "", "users", NewName, KeyFive));
        await Should.ThrowAsync<NotSupportedException>(async () => await commands.GenerateDeleteScript("db", "", "users", KeyFive));
    }
}
