using Aion.Components.Querying;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Events;
using Aion.Contracts.Queries;
using Aion.Test.TestDoubles;
using MudBlazor;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class QueryEditModeGuardTests
{
    private readonly EditingFixture _fixture = new();

    private QueryEditModeGuard CreateSut() => new(_fixture.Bus);

    private QueryModel CreateEditingQuery(string executedSql, params string[] resultColumns)
    {
        var query = new QueryModel
        {
            Query = executedSql,
            ConnectionId = _fixture.Connection.Id,
            DatabaseName = EditingFixture.DatabaseName,
            EditMetadata = new QueryEditMetadata
            {
                SourceTable = "users",
                SourceSchema = "public",
                SourceDatabase = EditingFixture.DatabaseName,
                ConnectionId = _fixture.Connection.Id,
                ColumnMetadata = EditingFixture.UserColumns(),
                IsEditMode = true
            }
        };
        query.SetResult(new QueryResult { Columns = resultColumns.ToList() });
        return query;
    }

    [Theory]
    [InlineData("SELECT * FROM \"public\".\"users\"\nLIMIT 1000;")]
    [InlineData("SELECT * FROM users WHERE name = 'Ada' ORDER BY id")]
    [InlineData("SELECT id, name FROM public.users")]
    public async Task SameTable_KeepsEditMode(string sql)
    {
        var query = CreateEditingQuery(sql, "id", "name");

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldNotBeNull();
        _fixture.Notifications().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("SELECT * FROM audit.users")]
    [InlineData("SELECT * FROM users u JOIN orders o ON o.user_id = u.id")]
    [InlineData("DELETE FROM users")]
    public async Task DifferentSql_EndsEditMode(string sql)
    {
        var query = CreateEditingQuery(sql, "id", "name");

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldBeNull();
        _fixture.Notifications().ShouldHaveSingleItem().Message.ShouldStartWith("Edit mode ended:");
    }

    [Fact]
    public async Task ResultWithoutPrimaryKey_EndsEditMode()
    {
        var query = CreateEditingQuery("SELECT name FROM users", "name");

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldBeNull();
        _fixture.Notifications().ShouldHaveSingleItem().Message.ShouldContain("primary key column(s) id");
    }

    [Fact]
    public async Task DifferentDatabase_EndsEditMode()
    {
        var query = CreateEditingQuery("SELECT * FROM users", "id", "name");
        query.DatabaseName = "other";

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldBeNull();
    }

    [Fact]
    public async Task ReRun_DiscardsPendingChangesForOldRows()
    {
        var query = CreateEditingQuery("SELECT * FROM users", "id", "name");
        var editState = query.EditMetadata!.EditState;
        editState.UpdateCell(0, "name", "changed", new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada" });

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldNotBeNull();
        editState.HasChanges.ShouldBeFalse();
        _fixture.Notifications().ShouldHaveSingleItem().Severity.ShouldBe(Severity.Warning);
    }

    // Read-only rows opened from one table keep its columns only so their foreign keys link.
    private QueryModel CreateReadOnlyQuery(string executedSql, params string[] resultColumns)
    {
        var query = CreateEditingQuery(executedSql, resultColumns);
        query.EditMetadata!.IsEditMode = false;
        return query;
    }

    [Theory]
    [InlineData("SELECT * FROM \"public\".\"users\"\nLIMIT 1000;")]
    [InlineData("SELECT name FROM users WHERE id > 3")]
    public async Task ReadOnlyRows_FromTheSameTable_KeepTheirForeignKeyLinks(string sql)
    {
        var query = CreateReadOnlyQuery(sql, "name");

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldNotBeNull();
        _fixture.Published().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("SELECT * FROM users u JOIN orders o ON o.user_id = u.id")]
    [InlineData("SELECT COUNT(*) FROM users")]
    public async Task ReadOnlyRows_FromOtherSql_DropTheirForeignKeyLinksQuietly(string sql)
    {
        var query = CreateReadOnlyQuery(sql, "id", "name");

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldBeNull();
        _fixture.Published().ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadOnlyRows_FromAnotherDatabase_DropTheirForeignKeyLinks()
    {
        var query = CreateReadOnlyQuery("SELECT * FROM users", "id", "name");
        query.DatabaseName = "other";

        await CreateSut().Consume(new QueryExecuted(query));

        query.EditMetadata.ShouldBeNull();
    }

    [Fact]
    public async Task QueryNotInEditMode_IsIgnored()
    {
        var query = new QueryModel { Query = "SELECT * FROM orders" };

        await CreateSut().Consume(new QueryExecuted(query));

        _fixture.Published().ShouldBeEmpty();
    }
}
