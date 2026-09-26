using Aion.Components.Querying;
using Aion.Components.Querying.Consumers;
using Aion.Components.Settings.Domains;
using Aion.Components.Shortcuts;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResultTableEditMenuTests : TestContext
{
    private static readonly string[] ColumnNames = ["id", "name", "note"];

    private readonly IRenderedComponent<MudPopoverProvider> _popovers;
    private readonly RowSelectionState _selection = new();

    private readonly QueryEditMetadata _metadata = new()
    {
        SourceTable = "products",
        IsEditMode = true,
        ColumnMetadata =
        [
            new ColumnInfo { Name = "id", DataType = "integer", IsPrimaryKey = true },
            new ColumnInfo { Name = "name", DataType = "text" },
            new ColumnInfo { Name = "note", DataType = "text", IsNullable = true }
        ]
    };

    private readonly QueryResult _result = new()
    {
        Columns = ColumnNames.ToList(),
        Rows =
        [
            new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada", ["note"] = "first" },
            new Dictionary<string, object> { ["id"] = 2, ["name"] = "Grace", ["note"] = "second" },
            new Dictionary<string, object> { ["id"] = 3, ["name"] = "Linus", ["note"] = "third" }
        ]
    };

    public QueryResultTableEditMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(Substitute.For<IMessageBus>());
        Services.AddSingleton(new ResultsSettings());
        Services.AddSingleton(AionKeyBindings.ForBrowser(isMac: false));

        _popovers = RenderComponent<MudPopoverProvider>();
    }

    private IRenderedComponent<QueryResultTable> Render() =>
        RenderComponent<QueryResultTable>(p => p
            .Add(x => x.Result, _result)
            .Add(x => x.QueryName, "Edit - products")
            .Add(x => x.EditMetadata, _metadata)
            .Add(x => x.SelectionState, _selection));

    private async Task OpenMenuAsync(IRenderedComponent<QueryResultTable> cut, int row, string column)
    {
        await cut.FindAll("tbody tr.mud-table-row")[row]
            .QuerySelectorAll("td")[Array.IndexOf(ColumnNames, column) + 1]
            .QuerySelector(".cell-content")!
            .ContextMenuAsync(new MouseEventArgs());
        _popovers.WaitForAssertion(() => _popovers.FindAll(".mud-menu-item").ShouldNotBeEmpty());
    }

    private List<string> MenuItems() =>
        _popovers.FindAll(".mud-menu-item .mud-menu-item-text").Select(i => i.TextContent.Trim()).ToList();

    private Task ChooseAsync(string item) =>
        _popovers.FindAll(".mud-menu-item")
            .First(li => li.QuerySelector(".mud-menu-item-text")?.TextContent.Trim() == item)
            .ClickAsync(new MouseEventArgs());

    [Fact]
    public async Task SetCellToNull_OnANullableCell_SetsItToNull()
    {
        var cut = Render();
        await OpenMenuAsync(cut, 0, "note");

        await ChooseAsync("Set Cell to NULL");

        _metadata.EditState.IsCellModified(0, "note").ShouldBeTrue();
        _metadata.EditState.GetEffectiveValue(0, "note", _result.Rows[0]).ShouldBeNull();
    }

    [Fact]
    public async Task NonNullableCell_OffersNoSetNull()
    {
        var cut = Render();

        await OpenMenuAsync(cut, 0, "name");

        MenuItems().ShouldNotContain("Set Cell to NULL");
    }

    [Fact]
    public async Task RevertCell_UndoesThatCellsChange()
    {
        _metadata.EditState.UpdateCell(0, "name", "Ada Lovelace", _result.Rows[0]);
        var cut = Render();
        await OpenMenuAsync(cut, 0, "name");

        await ChooseAsync("Revert Cell");

        _metadata.EditState.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSelectedRows_MarksEverySelectedRowForDelete()
    {
        _selection.ToggleRow(0, shiftKey: false, order: [0, 1, 2]);
        _selection.ToggleRow(2, shiftKey: false, order: [0, 1, 2]);
        var cut = Render();
        await OpenMenuAsync(cut, 0, "name");

        await ChooseAsync("Delete 2 Selected Rows");

        _metadata.EditState.DeletedRowCount.ShouldBe(2);
        _metadata.EditState.IsRowDeleted(0).ShouldBeTrue();
        _metadata.EditState.IsRowDeleted(1).ShouldBeFalse();
        _metadata.EditState.IsRowDeleted(2).ShouldBeTrue();
    }
}
