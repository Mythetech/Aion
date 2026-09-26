using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class EditModeEntryTests
{
    private static QueryState CreateQueryState(EditingFixture fixture) =>
        new(fixture.Bus, Substitute.For<IQuerySaveService>());

    private static void LoadColumns(EditingFixture fixture, List<ColumnInfo> columns)
    {
        fixture.Database.Tables = [new TableInfo("public", "users")];
        fixture.Database.TablesLoaded = true;
        fixture.Database.TableColumns["public.users"] = columns;
        fixture.Database.LoadedColumnTables.Add("public.users");
    }

    private static async Task<QueryModel> OpenTableEditor(EditingFixture fixture, List<ColumnInfo> columns)
    {
        LoadColumns(fixture, columns);
        var queryState = CreateQueryState(fixture);
        var sut = new TableEditorOpener(fixture.ConnectionState, queryState, fixture.Bus, NullLogger<TableEditorOpener>.Instance);

        await sut.Consume(new OpenTableEditor(fixture.Connection.Id, EditingFixture.DatabaseName, "public", "users"));

        return queryState.Active!;
    }

    [Fact]
    public async Task TableEditor_WithPrimaryKey_EntersEditModeBoundToTheConnection()
    {
        var fixture = new EditingFixture();

        var query = await OpenTableEditor(fixture, EditingFixture.UserColumns());

        query.EditMetadata.ShouldNotBeNull();
        query.EditMetadata.ConnectionId.ShouldBe(fixture.Connection.Id);
        query.EditMetadata.SourceTable.ShouldBe("users");
        fixture.Published().OfType<RunQuery>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TableEditor_WithoutPrimaryKey_OpensReadOnlyAndSaysWhy()
    {
        var fixture = new EditingFixture();

        var query = await OpenTableEditor(fixture, [new ColumnInfo { Name = "name" }]);

        query.EditMetadata.ShouldBeNull();
        query.Query.ShouldStartWith("SELECT * FROM");
        var notification = fixture.Notifications().ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Warning);
        notification.Message.ShouldContain("no primary key");
    }

    [Fact]
    public async Task TableEditor_ForProviderWithoutRowEditing_OpensReadOnly()
    {
        var fixture = new EditingFixture(supportsEditing: false);

        var query = await OpenTableEditor(fixture, EditingFixture.UserColumns());

        query.EditMetadata.ShouldBeNull();
        fixture.Notifications().ShouldHaveSingleItem().Message.ShouldContain("not supported");
    }

    private static async Task<(QueryModel Query, EditingFixture Fixture)> EnableFromQuery(string sql, bool supportsEditing = true)
    {
        var fixture = new EditingFixture(supportsEditing);
        LoadColumns(fixture, EditingFixture.UserColumns());
        var queryState = CreateQueryState(fixture);
        var query = queryState.AddQuery("q");
        query.ConnectionId = fixture.Connection.Id;
        query.DatabaseName = EditingFixture.DatabaseName;
        query.Query = sql;
        var sut = new QueryEditModeEnabler(fixture.ConnectionState, queryState, fixture.Bus, NullLogger<QueryEditModeEnabler>.Instance);

        await sut.Consume(new EnableEditModeFromQuery());

        return (query, fixture);
    }

    [Fact]
    public async Task EnableFromQuery_SimpleSelect_EntersEditMode()
    {
        var (query, fixture) = await EnableFromQuery("SELECT id, name FROM public.users WHERE id > 10");

        query.EditMetadata.ShouldNotBeNull();
        query.EditMetadata.ConnectionId.ShouldBe(fixture.Connection.Id);
        fixture.Notifications().ShouldHaveSingleItem().Severity.ShouldBe(Severity.Success);
    }

    [Theory]
    [InlineData("SELECT * FROM users u", "aliases")]
    [InlineData("SELECT * FROM users JOIN orders ON orders.user_id = users.id", "JOIN")]
    [InlineData("SELECT name FROM users", "primary key column(s) id")]
    public async Task EnableFromQuery_RejectsUnsafeQueries(string sql, string reason)
    {
        var (query, fixture) = await EnableFromQuery(sql);

        query.EditMetadata.ShouldBeNull();
        fixture.Notifications().ShouldHaveSingleItem().Message.ShouldContain(reason);
        fixture.Published().OfType<RunQuery>().ShouldBeEmpty();
    }

    [Fact]
    public async Task EnableFromQuery_ForProviderWithoutRowEditing_IsRefused()
    {
        var (query, fixture) = await EnableFromQuery("SELECT * FROM users", supportsEditing: false);

        query.EditMetadata.ShouldBeNull();
        fixture.Notifications().ShouldHaveSingleItem().Message.ShouldContain("not supported");
    }

    private static void ColumnsFailToLoad(EditingFixture fixture, string message) =>
        fixture.Provider.GetColumnsAsync(Arg.Any<string>(), Arg.Any<string>(), "public", "users")
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException(message));

    [Fact]
    public async Task TableEditor_WhenColumnsFailToLoad_ReportsTheErrorInsteadOfBlamingThePrimaryKey()
    {
        var fixture = new EditingFixture();
        ColumnsFailToLoad(fixture, "permission denied for table users");
        var queryState = CreateQueryState(fixture);
        var sut = new TableEditorOpener(fixture.ConnectionState, queryState, fixture.Bus, NullLogger<TableEditorOpener>.Instance);

        await sut.Consume(new OpenTableEditor(fixture.Connection.Id, EditingFixture.DatabaseName, "public", "users"));

        var notification = fixture.Notifications().ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Error);
        notification.Message.ShouldContain("permission denied for table users");
        fixture.Published().OfType<RunQuery>().ShouldBeEmpty();
    }

    [Fact]
    public async Task EnableFromQuery_WhenColumnsFailToLoad_ReportsTheError()
    {
        var fixture = new EditingFixture();
        fixture.Database.Tables = [new TableInfo("public", "users")];
        fixture.Database.TablesLoaded = true;
        ColumnsFailToLoad(fixture, "permission denied for table users");
        var queryState = CreateQueryState(fixture);
        var query = queryState.AddQuery("q");
        query.ConnectionId = fixture.Connection.Id;
        query.DatabaseName = EditingFixture.DatabaseName;
        query.Query = "SELECT * FROM public.users";
        var sut = new QueryEditModeEnabler(fixture.ConnectionState, queryState, fixture.Bus, NullLogger<QueryEditModeEnabler>.Instance);

        await sut.Consume(new EnableEditModeFromQuery());

        query.EditMetadata.ShouldBeNull();
        var notification = fixture.Notifications().ShouldHaveSingleItem();
        notification.Severity.ShouldBe(Severity.Error);
        notification.Message.ShouldContain("permission denied for table users");
    }
}
