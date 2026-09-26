using Aion.Components.Infrastructure.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Editing;
using Aion.Test.TestDoubles;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class PendingChangesBarTests : TestContext
{
    private readonly EditingFixture _fixture = new();
    private readonly EditState _editState = new() { IsEditMode = true };
    private bool _stoppedEditing;

    public PendingChangesBarTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_fixture.Bus);
        Services.AddSingleton(_fixture.CreateSqlBuilder());
    }

    private IRenderedComponent<PendingChangesBar> RenderBar(Aion.Contracts.Queries.Editing.EditableQueryResult? result = null) =>
        RenderComponent<PendingChangesBar>(p => p
            .Add(x => x.EditState, _editState)
            .Add(x => x.EditableResult, result ?? _fixture.CreateEditableResult())
            .Add(x => x.TableName, "users")
            .Add(x => x.OnStopEditing, () => _stoppedEditing = true));

    private void Rename(int row, string name)
    {
        var result = _fixture.CreateEditableResult();
        _editState.UpdateCell(row, "name", name, result.Rows[row]);
    }

    private void Delete(int row)
    {
        var result = _fixture.CreateEditableResult();
        _editState.DeleteRow(row, result.Rows[row]);
    }

    [Fact]
    public async Task WithoutChanges_SaysWhatIsBeingEdited_AndOffersToStopEditing()
    {
        var cut = RenderBar();

        cut.Find(".pending-bar-title").TextContent.ShouldBe("Editing users");
        cut.FindAll(".pending-bar-apply").ShouldBeEmpty();

        await cut.Find(".pending-bar-stop").ClickAsync(new MouseEventArgs());

        _stoppedEditing.ShouldBeTrue();
    }

    [Fact]
    public void WithChanges_CountsThemByKind_AndNamesTheApplyButtonByTheCount()
    {
        Rename(0, "Ada Lovelace");
        Delete(1);

        var cut = RenderBar();

        cut.Find(".pending-bar-summary").TextContent.Trim().ShouldBe("2 pending changes (1 update, 1 delete)");
        cut.Find(".pending-bar-apply").TextContent.Trim().ShouldBe("Apply 2 changes");
        cut.FindAll(".pending-bar-stop").ShouldBeEmpty();
    }

    [Fact]
    public void OneKindOfChange_IsCountedOnce()
    {
        Rename(0, "Ada Lovelace");

        var cut = RenderBar();

        cut.Find(".pending-bar-summary").TextContent.Trim().ShouldBe("1 pending change");
        cut.Find(".pending-bar-apply").TextContent.Trim().ShouldBe("Apply 1 change");
    }

    [Fact]
    public async Task FollowsChangesAsTheyAreMade()
    {
        var cut = RenderBar();

        await cut.InvokeAsync(() => Rename(0, "Ada Lovelace"));

        cut.Find(".pending-bar-summary").TextContent.Trim().ShouldBe("1 pending change");
    }

    [Fact]
    public async Task Apply_AppliesThePendingChangesToTheEditedTable()
    {
        Rename(0, "Ada Lovelace");
        var result = _fixture.CreateEditableResult();
        var cut = RenderBar(result);

        await cut.Find(".pending-bar-apply").ClickAsync(new MouseEventArgs());

        await _fixture.Bus.Received(1).PublishAsync(Arg.Is<ApplyPendingChanges>(c =>
            ReferenceEquals(c.EditState, _editState) && ReferenceEquals(c.EditableResult, result)));
    }

    [Fact]
    public async Task Discard_DropsEveryPendingChange_AndStaysInEditMode()
    {
        Rename(0, "Ada Lovelace");
        Delete(1);
        var cut = RenderBar();

        await cut.Find(".pending-bar-discard").ClickAsync(new MouseEventArgs());

        _editState.HasChanges.ShouldBeFalse();
        _stoppedEditing.ShouldBeFalse();
        cut.FindAll(".pending-bar-stop").ShouldHaveSingleItem();
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ReviewSqlAsync(IRenderedComponent<PendingChangesBar> cut)
    {
        var provider = RenderComponent<MudDialogProvider>();
        await cut.Find(".pending-bar-review").ClickAsync(new MouseEventArgs());
        provider.WaitForAssertion(() => provider.FindAll(".sql-review-caption").ShouldNotBeEmpty());
        return provider;
    }

    [Fact]
    public async Task ReviewSql_ShowsExactlyTheSqlTheApplierExecutes()
    {
        Rename(0, "O'Brien's Hub");
        Delete(1);
        var cut = RenderBar();

        var provider = await ReviewSqlAsync(cut);
        var reviewed = provider.FindAll("pre.sql-statement").Select(e => e.TextContent).ToList();

        var applier = new PendingChangesApplier(_fixture.CreateSqlBuilder(), _fixture.Bus, NullLogger<PendingChangesApplier>.Instance);
        await cut.InvokeAsync(() => applier.Consume(new ApplyPendingChanges(_editState, _fixture.CreateEditableResult())));

        reviewed.Count.ShouldBe(2);
        reviewed.ShouldBe(_fixture.ExecutedSql);
        reviewed[0].ShouldContain("'O''Brien''s Hub'");
        provider.Find(".sql-review-caption").TextContent.ShouldContain("one transaction");
    }

    [Fact]
    public async Task ReviewSql_ShowsTheValidationErrorInsteadOfSql_AndCannotApply()
    {
        Rename(0, "x");
        var cut = RenderBar(new Aion.Contracts.Queries.Editing.EditableQueryResult
        {
            Columns = ["name"],
            SourceTable = "users",
            SourceSchema = "public",
            SourceDatabase = EditingFixture.DatabaseName,
            ConnectionId = _fixture.Connection.Id,
            ColumnMetadata = EditingFixture.UserColumns()
        });

        var provider = RenderComponent<MudDialogProvider>();
        await cut.Find(".pending-bar-review").ClickAsync(new MouseEventArgs());

        provider.WaitForAssertion(() => provider.Markup.ShouldContain("primary key column(s) id"));
        provider.FindAll("pre.sql-statement").ShouldBeEmpty();
        provider.Find(".sql-review-apply").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task ReviewSql_ThenApply_AppliesTheChanges()
    {
        Rename(0, "Ada Lovelace");
        var cut = RenderBar();
        var provider = await ReviewSqlAsync(cut);

        await provider.Find(".sql-review-apply").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => _fixture.Bus.Received(1).PublishAsync(Arg.Any<ApplyPendingChanges>()));
        provider.FindAll(".sql-review").ShouldBeEmpty();
    }

    [Fact]
    public async Task ReviewSql_CopiesTheStatements()
    {
        Rename(0, "Ada Lovelace");
        Rename(1, "Grace Hopper");
        var cut = RenderBar();
        var provider = await ReviewSqlAsync(cut);

        await provider.Find(".sql-review-copy").ClickAsync(new MouseEventArgs());

        await _fixture.Bus.Received(1).PublishAsync(Arg.Is<CopyToClipboard>(c =>
            c.Text == "UPDATE \"public\".\"users\"\nSET \"name\" = 'Ada Lovelace'\nWHERE \"id\" = 1;\n\n" +
                      "UPDATE \"public\".\"users\"\nSET \"name\" = 'Grace Hopper'\nWHERE \"id\" = 2;"));
    }

    [Fact]
    public void Dispose_UnsubscribesFromEditState()
    {
        RenderBar();

        DisposeComponents();

        // A handler left subscribed would try to render off the dispatcher and throw here.
        Should.NotThrow(() => _editState.DiscardAllChanges());
    }
}
