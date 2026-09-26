using AngleSharp.Dom;
using Aion.Components.Querying;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Editing;
using Aion.Components.Settings.Domains;
using Aion.Components.Shortcuts;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResultTableEditingTests : TestContext
{
    private static readonly string[] ColumnNames = ["id", "name", "note", "price", "stock"];

    private readonly QueryEditMetadata _metadata = new()
    {
        SourceTable = "products",
        IsEditMode = true,
        ColumnMetadata =
        [
            new ColumnInfo { Name = "id", DataType = "integer", IsPrimaryKey = true },
            new ColumnInfo { Name = "name", DataType = "text" },
            new ColumnInfo { Name = "note", DataType = "text", IsNullable = true },
            new ColumnInfo { Name = "price", DataType = "numeric", IsNullable = true },
            new ColumnInfo { Name = "stock", DataType = "integer" }
        ]
    };

    private readonly QueryResult _result = new()
    {
        Columns = ColumnNames.ToList(),
        Rows =
        [
            new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada", ["note"] = "first", ["price"] = 9.5m, ["stock"] = 150L },
            new Dictionary<string, object> { ["id"] = 2, ["name"] = "Grace", ["note"] = null!, ["price"] = 12m, ["stock"] = 7L }
        ]
    };

    public QueryResultTableEditingTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(Substitute.For<IMessageBus>());
        Services.AddSingleton(new ResultsSettings());
        Services.AddSingleton(AionKeyBindings.ForBrowser(isMac: false));
    }

    private EditState Edits => _metadata.EditState;

    private IRenderedComponent<QueryResultTable> Render(RowSelectionState? selection = null) =>
        RenderComponent<QueryResultTable>(p =>
        {
            p.Add(x => x.Result, _result)
                .Add(x => x.QueryName, "Edit - products")
                .Add(x => x.EditMetadata, _metadata)
                .Add(x => x.EditState, _metadata.EditState);
            if (selection != null)
                p.Add(x => x.SelectionState, selection);
        });

    private static IElement Row(IRenderedComponent<QueryResultTable> cut, int row) =>
        cut.FindAll("tbody tr.mud-table-row")[row];

    private static IElement Cell(IRenderedComponent<QueryResultTable> cut, int row, string column) =>
        Row(cut, row).QuerySelectorAll("td")[Array.IndexOf(ColumnNames, column) + 1];

    private static IElement CellContent(IRenderedComponent<QueryResultTable> cut, int row, string column) =>
        Cell(cut, row, column).QuerySelector(".cell-content")!;

    private static Task ClickCellAsync(IRenderedComponent<QueryResultTable> cut, int row, string column) =>
        CellContent(cut, row, column).ClickAsync(new MouseEventArgs());

    private static IElement Editor(IRenderedComponent<QueryResultTable> cut) => cut.Find("input.cell-editor-input");

    private static Task TypeAsync(IRenderedComponent<QueryResultTable> cut, string text) =>
        Editor(cut).InputAsync(new ChangeEventArgs { Value = text });

    private static Task PressAsync(IRenderedComponent<QueryResultTable> cut, string key, bool shift = false, bool ctrl = false, bool meta = false) =>
        Editor(cut).KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift, CtrlKey = ctrl, MetaKey = meta });

    private object? PendingValue(int row, string column) =>
        Edits.GetEffectiveValue(row, column, _result.Rows[row]);

    [Fact]
    public void Cells_ShowTheirValuesWithoutTextFields_UntilOneIsEdited()
    {
        var cut = Render();

        cut.FindAll("input").ShouldBeEmpty();
        CellContent(cut, 0, "name").TextContent.Trim().ShouldBe("Ada");
        CellContent(cut, 1, "note").QuerySelector(".cell-null")!.TextContent.ShouldBe("NULL");
    }

    [Fact]
    public async Task ClickingAnEditableCell_OpensAnEditorWithItsValue()
    {
        var cut = Render();

        await ClickCellAsync(cut, 0, "name");

        cut.FindAll("input.cell-editor-input").ShouldHaveSingleItem().GetAttribute("value").ShouldBe("Ada");
    }

    [Fact]
    public async Task Enter_CommitsTheEdit_AndShowsTheOriginalStruckThrough()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        await TypeAsync(cut, "Ada Lovelace");
        await PressAsync(cut, "Enter");

        PendingValue(0, "name").ShouldBe("Ada Lovelace");
        cut.FindAll("input.cell-editor-input").ShouldBeEmpty();
        CellContent(cut, 0, "name").QuerySelector(".cell-value")!.TextContent.ShouldBe("Ada Lovelace");
        CellContent(cut, 0, "name").QuerySelector(".cell-original")!.TextContent.ShouldBe("Ada");
        Cell(cut, 0, "name").ClassList.ShouldContain("cell-modified");
        Cell(cut, 0, "note").ClassList.ShouldNotContain("cell-modified");
    }

    [Fact]
    public async Task Escape_CancelsTheEdit()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        await TypeAsync(cut, "Ada Lovelace");
        await PressAsync(cut, "Escape");

        Edits.HasChanges.ShouldBeFalse();
        cut.FindAll("input.cell-editor-input").ShouldBeEmpty();
    }

    [Fact]
    public async Task Tab_CommitsAndEditsTheNextEditableCell_ShiftTabGoesBack()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        await TypeAsync(cut, "Ada Lovelace");
        await PressAsync(cut, "Tab");

        PendingValue(0, "name").ShouldBe("Ada Lovelace");
        Editor(cut).GetAttribute("aria-label").ShouldBe("Edit note");

        await PressAsync(cut, "Tab", shift: true);
        Editor(cut).GetAttribute("aria-label").ShouldBe("Edit name");
    }

    [Fact]
    public async Task Tab_FromTheLastEditableCell_ContinuesOnTheNextRow()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "stock");

        await PressAsync(cut, "Tab");

        Editor(cut).GetAttribute("aria-label").ShouldBe("Edit name");
        Editor(cut).GetAttribute("value").ShouldBe("Grace");
    }

    [Fact]
    public async Task LeavingACellWithTheSameText_RecordsNoChange()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "stock");

        await TypeAsync(cut, "150");
        await PressAsync(cut, "Enter");

        Edits.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task Blur_CommitsTheEdit()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        await TypeAsync(cut, "Ada Lovelace");
        await Editor(cut).BlurAsync(new FocusEventArgs());

        PendingValue(0, "name").ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task PrimaryKeyCell_DoesNotOpenAnEditor()
    {
        var cut = Render();

        await ClickCellAsync(cut, 0, "id");

        cut.FindAll("input").ShouldBeEmpty();
        CellContent(cut, 0, "id").GetAttribute("title").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task EnterOnAFocusedCell_OpensItsEditor()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "id");

        await CellContent(cut, 0, "id").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await CellContent(cut, 0, "name").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Editor(cut).GetAttribute("aria-label").ShouldBe("Edit name");
    }

    [Fact]
    public async Task ArrowKeys_MoveWhichCellHasFocus()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "id");

        await CellContent(cut, 0, "id").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        CellContent(cut, 1, "id").GetAttribute("tabindex").ShouldBe("0");
        CellContent(cut, 0, "id").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public async Task EnterInTheEditor_MovesFocusToTheCellBelow()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        await PressAsync(cut, "Enter");

        CellContent(cut, 1, "name").GetAttribute("tabindex").ShouldBe("0");
    }

    [Fact]
    public async Task TypingOnAFocusedCell_StartsEditingWithThatCharacter()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "id");
        await CellContent(cut, 0, "id").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        await CellContent(cut, 0, "name").KeyDownAsync(new KeyboardEventArgs { Key = "Z" });

        Editor(cut).GetAttribute("value").ShouldBe("Z");
    }

    [Fact]
    public void RowMarkedForDelete_IsStruckThrough()
    {
        Edits.DeleteRow(1, _result.Rows[1]);

        var cut = Render();

        Row(cut, 1).ClassList.ShouldContain("row-deleted");
        Row(cut, 0).ClassList.ShouldNotContain("row-deleted");
    }

    [Fact]
    public async Task CellsOfARowMarkedForDelete_DoNotOpenAnEditor()
    {
        Edits.DeleteRow(1, _result.Rows[1]);
        var cut = Render();

        await ClickCellAsync(cut, 1, "name");

        cut.FindAll("input").ShouldBeEmpty();
    }

    [Fact]
    public async Task ClickingACell_DoesNotSelectItsRow()
    {
        var selection = new RowSelectionState();
        var cut = Render(selection);

        await ClickCellAsync(cut, 1, "name");
        await ClickCellAsync(cut, 0, "id");

        selection.HasSelection.ShouldBeFalse();
    }

    [Fact]
    public async Task RowNumbers_StillSelectRows()
    {
        var selection = new RowSelectionState();
        var cut = Render(selection);

        await Row(cut, 1).QuerySelector(".row-number")!.ClickAsync(new MouseEventArgs());

        selection.SelectedIndices.ShouldBe([1]);
    }

    [Fact]
    public async Task CtrlZero_InANullableCell_SetsNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "note");

        await PressAsync(cut, "0", ctrl: true);

        Edits.IsCellModified(0, "note").ShouldBeTrue();
        PendingValue(0, "note").ShouldBeNull();
        CellContent(cut, 0, "note").QuerySelector(".cell-null")!.TextContent.ShouldBe("NULL");
        CellContent(cut, 0, "note").QuerySelector(".cell-original")!.TextContent.ShouldBe("first");
    }

    [Fact]
    public async Task CmdZero_InANullableCell_SetsNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "note");

        await PressAsync(cut, "0", meta: true);

        PendingValue(0, "note").ShouldBeNull();
        Edits.IsCellModified(0, "note").ShouldBeTrue();
    }

    [Fact]
    public async Task CtrlZero_OnAFocusedCell_SetsNullWithoutOpeningAnEditor()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "id");
        await CellContent(cut, 0, "id").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        await CellContent(cut, 0, "name").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        await CellContent(cut, 0, "note").KeyDownAsync(new KeyboardEventArgs { Key = "0", CtrlKey = true });
        await CellContent(cut, 0, "name").KeyDownAsync(new KeyboardEventArgs { Key = "0", CtrlKey = true });

        cut.FindAll("input").ShouldBeEmpty();
        PendingValue(0, "note").ShouldBeNull();
        Edits.IsCellModified(0, "name").ShouldBeFalse();
    }

    [Fact]
    public async Task SetNullButton_SetsNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "note");

        await cut.Find(".cell-editor-null").ClickAsync(new MouseEventArgs());

        Edits.IsCellModified(0, "note").ShouldBeTrue();
        PendingValue(0, "note").ShouldBeNull();
    }

    [Fact]
    public async Task ClearingATextCell_StoresAnEmptyStringNotNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "note");

        await TypeAsync(cut, "");
        await PressAsync(cut, "Enter");

        Edits.IsCellModified(0, "note").ShouldBeTrue();
        PendingValue(0, "note").ShouldBe("");
        CellContent(cut, 0, "note").QuerySelectorAll(".cell-null").ShouldBeEmpty();
    }

    [Fact]
    public async Task EditingANullCell_ShowsNullUntilSomethingIsTyped()
    {
        var cut = Render();
        await ClickCellAsync(cut, 1, "note");

        Editor(cut).GetAttribute("placeholder").ShouldBe("NULL");
        await PressAsync(cut, "Enter");

        Edits.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task BackspaceInANullTextCell_MakesItAnEmptyString()
    {
        var cut = Render();
        await ClickCellAsync(cut, 1, "note");

        await PressAsync(cut, "Backspace");

        Editor(cut).GetAttribute("placeholder").ShouldBe("empty string");
        await PressAsync(cut, "Enter");
        PendingValue(1, "note").ShouldBe("");
    }

    [Fact]
    public async Task NonNullableColumn_OffersNoWayToSetNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "name");

        cut.FindAll(".cell-editor-null").ShouldBeEmpty();
        await PressAsync(cut, "0", ctrl: true);
        await PressAsync(cut, "Enter");

        Edits.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearingANullableNumber_StoresNull()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "price");

        await TypeAsync(cut, "");
        await PressAsync(cut, "Enter");

        Edits.IsCellModified(0, "price").ShouldBeTrue();
        PendingValue(0, "price").ShouldBeNull();
    }

    [Fact]
    public async Task ClearingARequiredNumber_KeepsTheEditorOpenAndMarksItInvalid()
    {
        var cut = Render();
        await ClickCellAsync(cut, 0, "stock");

        await TypeAsync(cut, "");
        await PressAsync(cut, "Enter");

        Edits.HasChanges.ShouldBeFalse();
        Editor(cut).GetAttribute("aria-invalid").ShouldBe("true");
    }
}
