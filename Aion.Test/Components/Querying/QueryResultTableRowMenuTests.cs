using System.Text.Json;
using AngleSharp.Dom;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.RequestContextPanel.Commands;
using Aion.Components.Settings.Domains;
using Aion.Components.Shared;
using Aion.Components.Shortcuts;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResultTableRowMenuTests : TestContext
{
    private static readonly string[] ColumnNames = ["id", "name", "category_id"];

    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;
    private readonly RowSelectionState _selection = new();
    private readonly Guid _connectionId = Guid.NewGuid();

    private readonly QueryResult _result = new()
    {
        Columns = ColumnNames.ToList(),
        Rows =
        [
            new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada", ["category_id"] = 7 },
            new Dictionary<string, object> { ["id"] = 2, ["name"] = "Grace", ["category_id"] = null! },
            new Dictionary<string, object> { ["id"] = 3, ["name"] = "Linus", ["category_id"] = 9 }
        ]
    };

    public QueryResultTableRowMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_bus);
        Services.AddSingleton(new ResultsSettings());
        Services.AddSingleton(AionKeyBindings.ForBrowser(isMac: false));

        _popovers = RenderComponent<MudPopoverProvider>();
    }

    private IRenderedComponent<QueryResultTable> Render(QueryEditMetadata? metadata = null) =>
        RenderComponent<QueryResultTable>(p =>
        {
            p.Add(x => x.Result, _result)
                .Add(x => x.QueryName, "Query1")
                .Add(x => x.SelectionState, _selection)
                .Add(x => x.ConnectionId, _connectionId)
                .Add(x => x.DatabaseName, "shop");
            if (metadata != null)
                p.Add(x => x.EditMetadata, metadata);
        });

    // Rows from one known table carry its columns, and so its foreign keys, without being in edit mode.
    private static QueryEditMetadata ProductsTable() => new()
    {
        SourceTable = "products",
        ColumnMetadata =
        [
            new ColumnInfo { Name = "id", IsPrimaryKey = true },
            new ColumnInfo { Name = "name" },
            new ColumnInfo
            {
                Name = "category_id",
                ForeignKey = new ForeignKeyInfo { ColumnName = "category_id", ReferencedTable = "categories", ReferencedColumn = "id" }
            }
        ]
    };

    private static IElement Row(IRenderedComponent<QueryResultTable> cut, int row) =>
        cut.FindAll("tbody tr.mud-table-row")[row];

    // bUnit doesn't bubble events, so a right-click on a cell is raised on the cell, which notes which cell it was,
    // and then on its row, which opens the menu: the order the browser delivers them in.
    private async Task RightClickAsync(IRenderedComponent<QueryResultTable> cut, int row, string? column = null)
    {
        if (column != null)
        {
            await Row(cut, row).QuerySelectorAll("td")[Array.IndexOf(ColumnNames, column) + 1]
                .QuerySelector(".cell-content")!
                .ContextMenuAsync(new MouseEventArgs { Button = 2 });
        }

        await Row(cut, row).ContextMenuAsync(new MouseEventArgs { Button = 2, ClientX = 40, ClientY = 60 });
        _popovers.WaitForAssertion(() => _popovers.FindAll(".mud-menu-item").ShouldNotBeEmpty());
    }

    private List<string> MenuItems() =>
        _popovers.FindAll(".mud-menu-item .mud-menu-item-text").Select(i => i.TextContent.Trim()).ToList();

    private IElement MenuItem(string text) =>
        _popovers.FindAll(".mud-menu-item").First(li => li.QuerySelector(".mud-menu-item-text")?.TextContent.Trim() == text);

    private Task ChooseAsync(string text) => MenuItem(text).ClickAsync(new MouseEventArgs());

    private T Published<T>() =>
        (T)_bus.ReceivedCalls().Single(c => c.GetArguments()[0] is T).GetArguments()[0]!;

    [Fact]
    public async Task RightClickingARowOutsideItsCells_OpensTheRowsMenu()
    {
        var cut = Render();

        await RightClickAsync(cut, 1);

        MenuItems().ShouldContain("Copy row");
        MenuItems().ShouldNotContain("Copy cell");
    }

    [Fact]
    public async Task CopyCell_CopiesTheCellThatWasRightClicked()
    {
        var cut = Render();
        await RightClickAsync(cut, 1, "name");

        await ChooseAsync("Copy cell");

        Published<CopyCellToClipboard>().Value.ShouldBe("Grace");
    }

    [Fact]
    public async Task CopyCell_OnANullCell_IsUnavailable()
    {
        var cut = Render();

        await RightClickAsync(cut, 1, "category_id");

        MenuItem("Copy cell").GetAttribute("aria-disabled").ShouldBe("true");
    }

    [Fact]
    public async Task CopyRow_CopiesTheRightClickedRowWithItsHeaders()
    {
        var cut = Render();
        await RightClickAsync(cut, 2, "id");

        await ChooseAsync("Copy row");

        var copied = Published<CopyRowToClipboard>();
        copied.Row.ShouldBeSameAs(_result.Rows[2]);
        copied.Format.ShouldBe("Csv");
        copied.Headers.ShouldBe(ColumnNames);
    }

    [Fact]
    public async Task CopyRowAsJson_CopiesTheRightClickedRowAsJson()
    {
        var cut = Render();
        await RightClickAsync(cut, 0);

        await ChooseAsync("Copy row as JSON");

        var copied = Published<CopyRowToClipboard>();
        copied.Row.ShouldBeSameAs(_result.Rows[0]);
        copied.Format.ShouldBe("Json");
    }

    [Fact]
    public async Task RightClickingARowInAMultiRowSelection_CopiesTheSelectedRows()
    {
        _selection.SelectAll([0, 2]);
        var cut = Render();
        await RightClickAsync(cut, 2, "name");

        await ChooseAsync("Copy 2 selected rows");

        Published<CopySelectedRowsToClipboard>().Rows.ShouldBe([_result.Rows[0], _result.Rows[2]]);
    }

    [Fact]
    public async Task RightClickingARowOutsideTheSelection_OffersNothingForTheSelection()
    {
        _selection.SelectAll([0, 2]);
        var cut = Render();

        await RightClickAsync(cut, 1, "name");

        MenuItems().ShouldNotContain(item => item.Contains("selected"));
    }

    [Fact]
    public async Task RightClickingTheOnlySelectedRow_OffersToCopyTheRowButNotTheSelection()
    {
        _selection.SelectAll([1]);
        var cut = Render();

        await RightClickAsync(cut, 1);

        MenuItems().ShouldContain("Copy row");
        MenuItems().ShouldNotContain(item => item.StartsWith("Copy 1"));
    }

    [Fact]
    public async Task RightClicking_LeavesTheSelectionAsItWas()
    {
        _selection.SelectAll([0]);
        var cut = Render();

        await RightClickAsync(cut, 1, "name");

        _selection.SelectedIndices.ShouldBe([0]);
    }

    [Fact]
    public async Task SelectRow_SelectsTheRightClickedRow_AndDeselectRowClearsIt()
    {
        _selection.SelectAll([0]);
        var cut = Render();

        await RightClickAsync(cut, 1, "name");
        await ChooseAsync("Select row");
        _selection.SelectedIndices.OrderBy(i => i).ShouldBe([0, 1]);

        await RightClickAsync(cut, 1, "name");
        await ChooseAsync("Deselect row");
        _selection.SelectedIndices.ShouldBe([0]);
    }

    [Fact]
    public async Task ViewRowAsJson_OpensTheRowInTheJsonViewer()
    {
        var cut = Render();
        await RightClickAsync(cut, 1);

        await ChooseAsync("View row as JSON");

        var detail = Published<OpenJsonDetailView>().ColumnDetail;
        detail.QueryName.ShouldBe("Query1");
        detail.Column.ShouldBe("Row 2");
        using var json = JsonDocument.Parse(detail.Json);
        json.RootElement.GetProperty("name").GetString().ShouldBe("Grace");
        json.RootElement.GetProperty("category_id").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task RowsFromAKnownTable_OfferTheRowsTheirForeignKeysReference()
    {
        var cut = Render(ProductsTable());
        await RightClickAsync(cut, 2, "name");

        await ChooseAsync("Show categories row");

        var detail = Published<OpenForeignKeyView>().ForeignKeyDetail;
        detail.ReferencedTable.ShouldBe("categories");
        detail.ForeignKeyValue.ShouldBe(9);
        detail.ConnectionId.ShouldBe(_connectionId);
    }

    [Fact]
    public async Task ANullForeignKey_OffersNoRowToShow()
    {
        var cut = Render(ProductsTable());

        await RightClickAsync(cut, 1, "name");

        MenuItems().ShouldNotContain(item => item.StartsWith("Show "));
    }

    [Fact]
    public async Task RowsFromAnUnknownTable_OfferNoForeignKeyRows()
    {
        var cut = Render();

        await RightClickAsync(cut, 0, "category_id");

        MenuItems().ShouldNotContain(item => item.StartsWith("Show "));
    }

    [Fact]
    public async Task ExportSelectedRows_ExportsTheSelection()
    {
        _selection.SelectAll([0, 2]);
        var cut = Render();
        await RightClickAsync(cut, 0);

        await ChooseAsync("Export 2 selected rows to CSV");

        var exported = Published<ExportSelectedRows>();
        exported.Rows.ShouldBe([_result.Rows[0], _result.Rows[2]]);
        exported.Format.ShouldBe("Csv");
    }

    [Fact]
    public async Task TheRightClickedRow_IsMarkedWhileItsMenuIsOpen()
    {
        var cut = Render();

        await RightClickAsync(cut, 1, "name");

        cut.FindAll("tbody tr.mud-table-row").Select(tr => tr.ClassList.Contains("row-context")).ShouldBe([false, true, false]);
    }

    [Fact]
    public async Task TheMenusItems_SitWhereTheyTakeFocusAsItOpens()
    {
        var cut = Render();

        await RightClickAsync(cut, 1, "name");

        _popovers.FindAll(".context-menu-focus .mud-menu-item").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Escape_ClosesTheMenu_AndHandsFocusBackToTheGrid()
    {
        var cut = Render();
        await RightClickAsync(cut, 1, "name");
        var focusCalls = FocusCalls();

        await _popovers.Find(".context-menu-focus").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        _popovers.WaitForAssertion(() => _popovers.FindAll(".mud-menu-item").ShouldBeEmpty());
        cut.WaitForAssertion(() => FocusCalls().ShouldBeGreaterThan(focusCalls));
        Row(cut, 1).ClassList.ShouldNotContain("row-context");
    }

    [Fact]
    public async Task ChoosingAnItem_HandsFocusBackToTheGrid()
    {
        var cut = Render();
        await RightClickAsync(cut, 1, "name");
        var focusCalls = FocusCalls();

        await ChooseAsync("Copy row");

        cut.WaitForAssertion(() => FocusCalls().ShouldBeGreaterThan(focusCalls));
    }

    private int FocusCalls() => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");
}
