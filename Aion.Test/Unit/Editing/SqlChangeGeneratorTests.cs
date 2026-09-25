using Aion.Contracts.Database;
using Aion.Contracts.Queries.Editing;
using Aion.Core.Database.MySql;
using Aion.Core.Database.PostgreSQL;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class SqlChangeGeneratorTests
{
    private readonly SqlChangeGenerator _sut = new();

    private static EditableQueryResult CreateResult(params string[] columns)
    {
        return new EditableQueryResult
        {
            Columns = columns.ToList(),
            SourceTable = "users",
            SourceSchema = "public",
            SourceDatabase = "app",
            ColumnMetadata =
            [
                new ColumnInfo { Name = "id", IsPrimaryKey = true, IsIdentity = true },
                new ColumnInfo { Name = "name" },
                new ColumnInfo { Name = "note" }
            ]
        };
    }

    private static PendingChange Rename(object? id, string newName) => PendingChange.CreateUpdate(
        0,
        new Dictionary<string, object?> { ["id"] = id, ["name"] = "old", ["note"] = null },
        new Dictionary<string, object?> { ["id"] = id, ["name"] = newName, ["note"] = null });

    [Fact]
    public async Task Update_TargetsTheOriginalPrimaryKeyThroughTheProviderCommands()
    {
        var result = CreateResult("id", "name", "note");

        var generation = await _sut.GenerateSqlAsync(result, [Rename(0, "O'Brien's Hub")], new MySqlCommands());

        generation.IsValid.ShouldBeTrue();
        generation.Statements.Count.ShouldBe(1);
        generation.Statements[0].Sql.ShouldBe("UPDATE `users`\nSET `name` = 'O''Brien''s Hub'\nWHERE `id` = 0;");
        generation.Statements[0].ExpectsSingleRow.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_PassesStructuredKeyValuesToCommands()
    {
        var commands = Substitute.For<IStandardDatabaseCommands>();
        commands.GenerateUpdateScript(default!, default!, default!, default!, default!).ReturnsForAnyArgs("sql");

        await _sut.GenerateSqlAsync(CreateResult("id", "name", "note"), [Rename(42, "new")], commands);

        await commands.Received(1).GenerateUpdateScript(
            "app",
            "public",
            "users",
            Arg.Is<IEnumerable<ColumnValue>>(v => v.Single() == new ColumnValue("name", "new")),
            Arg.Is<IEnumerable<ColumnValue>>(k => k.Single() == new ColumnValue("id", 42)));
    }

    [Fact]
    public async Task Update_ResultWithoutPrimaryKeyColumn_IsRejected()
    {
        var generation = await _sut.GenerateSqlAsync(CreateResult("name", "note"), [Rename(1, "new")], new PostgreSqlCommands());

        generation.IsValid.ShouldBeFalse();
        generation.ValidationError.ShouldNotBeNull().ShouldContain("id");
        generation.Statements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_NullPrimaryKey_IsRejected()
    {
        var generation = await _sut.GenerateSqlAsync(CreateResult("id", "name", "note"), [Rename(null, "new")], new PostgreSqlCommands());

        generation.IsValid.ShouldBeFalse();
        generation.ValidationError.ShouldNotBeNull().ShouldContain("NULL");
    }

    [Fact]
    public async Task Update_ColumnOutsideTheTable_IsRejected()
    {
        var change = PendingChange.CreateUpdate(
            0,
            new Dictionary<string, object?> { ["id"] = 1, ["upper_name"] = "A" },
            new Dictionary<string, object?> { ["id"] = 1, ["upper_name"] = "B" });

        var generation = await _sut.GenerateSqlAsync(CreateResult("id", "upper_name"), [change], new PostgreSqlCommands());

        generation.IsValid.ShouldBeFalse();
        generation.ValidationError.ShouldNotBeNull().ShouldContain("upper_name");
    }

    [Fact]
    public async Task Update_MatchesKeyColumnCaseInsensitively()
    {
        var change = PendingChange.CreateUpdate(
            0,
            new Dictionary<string, object?> { ["ID"] = 3, ["Name"] = "a" },
            new Dictionary<string, object?> { ["ID"] = 3, ["Name"] = "b" });

        var generation = await _sut.GenerateSqlAsync(CreateResult("ID", "Name"), [change], new PostgreSqlCommands());

        generation.Statements.Single().Sql.ShouldBe("UPDATE \"public\".\"users\"\nSET \"name\" = 'b'\nWHERE \"id\" = 3;");
    }

    [Fact]
    public async Task Delete_TargetsTheKey()
    {
        var change = PendingChange.CreateDelete(2, new Dictionary<string, object?> { ["id"] = 9, ["name"] = "x" });

        var generation = await _sut.GenerateSqlAsync(CreateResult("id", "name"), [change], new PostgreSqlCommands());

        generation.Statements.Single().Sql.ShouldBe("DELETE FROM \"public\".\"users\"\nWHERE \"id\" = 9;");
        generation.Statements.Single().ExpectsSingleRow.ShouldBeTrue();
    }

    [Fact]
    public async Task Insert_SkipsIdentityColumns_AndDoesNotExpectAKeyMatch()
    {
        var change = PendingChange.CreateInsert(3, new Dictionary<string, object?> { ["id"] = null, ["name"] = "new" });

        var generation = await _sut.GenerateSqlAsync(CreateResult("id", "name"), [change], new PostgreSqlCommands());

        generation.Statements.Single().Sql.ShouldBe("INSERT INTO \"public\".\"users\"\n(\"name\")\nVALUES ('new');");
        generation.Statements.Single().ExpectsSingleRow.ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleStatements_RequireATransaction()
    {
        var generation = await _sut.GenerateSqlAsync(
            CreateResult("id", "name", "note"),
            [Rename(1, "a"), Rename(2, "b")],
            new PostgreSqlCommands());

        generation.RequiresTransaction.ShouldBeTrue();
        generation.Statements.Select(s => s.Change.OriginalValues["id"]).ShouldBe(new object?[] { 1, 2 });
    }

    [Fact]
    public async Task CommandsThatRefuseEdits_SurfaceAsValidationError()
    {
        var generation = await _sut.GenerateSqlAsync(
            CreateResult("id", "name", "note"),
            [Rename(1, "a")],
            new Aion.Core.Database.LiteDB.LiteDBCommands());

        generation.IsValid.ShouldBeFalse();
        generation.ValidationError.ShouldNotBeNull().ShouldContain("LiteDB");
    }
}
