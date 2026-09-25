using Aion.Components.Querying.Editing;
using Aion.Contracts.Database;
using Aion.Core.Database.MySql;
using Aion.Core.Database.PostgreSQL;
using Aion.Core.Database.SqlServer;
using Aion.Web.Providers;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class EditableQueryParserTests
{
    public static TheoryData<IStandardDatabaseCommands, string, string?> GeneratedSelects => new()
    {
        { new PostgreSqlCommands(), "public", "public" },
        { new PGliteCommands(), "public", null },
        { new SqliteWasmCommands(), "", null },
        { new MySqlCommands(), "", null },
        { new SqlServerCommands(), "dbo", "dbo" },
    };

    [Theory]
    [MemberData(nameof(GeneratedSelects))]
    public async Task Parse_AcceptsTheSelectEveryProviderGenerates(IStandardDatabaseCommands commands, string schema, string? expectedSchema)
    {
        var sql = await commands.GenerateSelectTopScript("db", schema, "users", 1000);

        var result = EditableQueryParser.Parse(sql);

        result.Error.ShouldBeNull();
        result.Target.ShouldBe(new EditableQueryTarget(expectedSchema, "users", null));
    }

    [Theory]
    [InlineData("select * from users where name = 'O''Brien' order by id limit 10;")]
    [InlineData("SELECT * FROM users -- recent first\nORDER BY created_at DESC")]
    [InlineData("SELECT * FROM users /* all */ WHERE id IN (SELECT user_id FROM orders)")]
    [InlineData("SELECT TOP (50) * FROM users")]
    [InlineData("SELECT * FROM users OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY")]
    public void Parse_AcceptsFiltersAndPaging(string sql)
    {
        EditableQueryParser.Parse(sql).Target!.Table.ShouldBe("users");
    }

    [Fact]
    public void Parse_ReturnsExplicitColumns()
    {
        var result = EditableQueryParser.Parse("SELECT id, \"Display Name\", `email` FROM app.users");

        result.Target.ShouldNotBeNull();
        result.Target.Schema.ShouldBe("app");
        result.Target.Columns!.ShouldBe(new[] { "id", "Display Name", "email" });
    }

    [Fact]
    public void Parse_UnescapesQuotedIdentifiers()
    {
        EditableQueryParser.Parse("SELECT * FROM [we]]ird]").Target!.Table.ShouldBe("we]ird");
        EditableQueryParser.Parse("SELECT * FROM \"we\"\"ird\"").Target!.Table.ShouldBe("we\"ird");
    }

    [Theory]
    [InlineData("SELECT * FROM users u")]
    [InlineData("SELECT * FROM users AS u")]
    [InlineData("SELECT * FROM users WITH (NOLOCK)")]
    public void Parse_RejectsTableAliases(string sql)
    {
        EditableQueryParser.Parse(sql).Error.ShouldBe("Edit mode does not support table aliases or table hints");
    }

    [Theory]
    [InlineData("SELECT * FROM users JOIN orders ON orders.user_id = users.id")]
    [InlineData("SELECT * FROM users u INNER JOIN orders o ON o.user_id = u.id")]
    [InlineData("SELECT * FROM users, orders")]
    public void Parse_RejectsJoins(string sql)
    {
        EditableQueryParser.Parse(sql).Error.ShouldBe("Edit mode does not support JOINs or multiple tables");
    }

    [Theory]
    [InlineData("SELECT id, name AS title FROM users")]
    [InlineData("SELECT id, name title FROM users")]
    [InlineData("SELECT id, upper(name) FROM users")]
    [InlineData("SELECT users.id FROM users")]
    [InlineData("SELECT DISTINCT * FROM users")]
    [InlineData("SELECT COUNT(*) FROM users")]
    [InlineData("UPDATE users SET name = 'x'")]
    [InlineData("WITH u AS (SELECT * FROM users) SELECT * FROM u")]
    [InlineData("SELECT * FROM db.dbo.users")]
    [InlineData("SELECT * FROM users; DELETE FROM users")]
    [InlineData("SELECT * FROM users WHERE id = 1 UNION SELECT * FROM admins")]
    [InlineData("SELECT * FROM users GROUP BY id")]
    [InlineData("SELECT * FROM users WHERE name = 'x\\' UNION SELECT * FROM admins --'")]
    [InlineData("SELECT * FROM users WHERE 1=1 /*! UNION SELECT * FROM admins */")]
    [InlineData("SELECT * FROM users WHERE id = 1--1\nUNION SELECT * FROM admins")]
    [InlineData("SELECT * FROM users WHERE 1=1 # '\nUNION SELECT * FROM admins WHERE 'a'='a'")]
    [InlineData("SELECT * FROM users WHERE name = $$x$$")]
    [InlineData("SELECT * FROM users WHERE name = 'unterminated")]
    [InlineData("")]
    public void Parse_RejectsAnythingThatIsNotASimpleSingleTableSelect(string sql)
    {
        var result = EditableQueryParser.Parse(sql);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrEmpty();
    }
}
