using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Editing;
using Aion.Test.TestDoubles;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class PendingChangesPanelTests : TestContext
{
    private readonly EditingFixture _fixture = new();

    public PendingChangesPanelTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_fixture.CreateSqlBuilder());
    }

    private IRenderedComponent<PendingChangesPanel> RenderPanel(EditState editState)
    {
        return RenderComponent<PendingChangesPanel>(p => p
            .Add(x => x.EditState, editState)
            .Add(x => x.EditableResult, _fixture.CreateEditableResult()));
    }

    private static async Task ShowSqlAsync(IRenderedComponent<PendingChangesPanel> cut)
    {
        await cut.FindAll("button").First(b => b.TextContent.Contains("Show SQL")).ClickAsync(new());
    }

    [Fact]
    public async Task Preview_ShowsExactlyTheSqlTheApplierExecutes()
    {
        var editState = new EditState { IsEditMode = true };
        var result = _fixture.CreateEditableResult();
        editState.UpdateCell(0, "name", "O'Brien's Hub", result.Rows[0]);
        editState.DeleteRow(1, result.Rows[1]);
        var cut = RenderPanel(editState);

        await ShowSqlAsync(cut);
        var previewed = cut.FindAll("pre.sql-statement").Select(e => e.TextContent).ToList();

        var applier = new PendingChangesApplier(_fixture.CreateSqlBuilder(), _fixture.Bus, NullLogger<PendingChangesApplier>.Instance);
        await cut.InvokeAsync(() => applier.Consume(new ApplyPendingChanges(editState, result)));

        previewed.Count.ShouldBe(2);
        previewed.ShouldBe(_fixture.ExecutedSql);
        previewed[0].ShouldContain("'O''Brien''s Hub'");
    }

    [Fact]
    public async Task Preview_RefreshesWhenChangesAreAdded()
    {
        var editState = new EditState { IsEditMode = true };
        var result = _fixture.CreateEditableResult();
        editState.UpdateCell(0, "name", "first", result.Rows[0]);
        var cut = RenderPanel(editState);
        await ShowSqlAsync(cut);

        await cut.InvokeAsync(() => editState.UpdateCell(1, "name", "second", result.Rows[1]));

        cut.WaitForAssertion(() => cut.FindAll("pre.sql-statement").Count.ShouldBe(2));
    }

    [Fact]
    public async Task Preview_ShowsValidationErrorInsteadOfSql()
    {
        var editState = new EditState { IsEditMode = true };
        var result = _fixture.CreateEditableResult();
        editState.UpdateCell(0, "name", "x", result.Rows[0]);
        var cut = RenderComponent<PendingChangesPanel>(p => p
            .Add(x => x.EditState, editState)
            .Add(x => x.EditableResult, new Aion.Contracts.Queries.Editing.EditableQueryResult
            {
                Columns = ["name"],
                SourceTable = "users",
                SourceSchema = "public",
                SourceDatabase = EditingFixture.DatabaseName,
                ConnectionId = _fixture.Connection.Id,
                ColumnMetadata = EditingFixture.UserColumns()
            }));

        await ShowSqlAsync(cut);

        cut.FindAll("pre.sql-statement").ShouldBeEmpty();
        cut.Markup.ShouldContain("primary key column(s) id");
    }

    [Fact]
    public void Dispose_UnsubscribesFromEditState()
    {
        var editState = new EditState { IsEditMode = true };
        RenderPanel(editState);

        DisposeComponents();

        // A handler left subscribed would try to render off the dispatcher and throw here.
        Should.NotThrow(() => editState.DiscardAllChanges());
    }
}
