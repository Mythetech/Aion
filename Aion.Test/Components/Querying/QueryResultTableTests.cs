using AngleSharp.Dom;
using Aion.Components.Querying;
using Aion.Components.Settings.Domains;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryResultTableTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ResultsSettings _settings = new();

    public QueryResultTableTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_bus);
        Services.AddSingleton(_settings);
    }

    private static QueryResult Numbered(int count) => new()
    {
        Columns = ["id", "name"],
        Rows = Enumerable.Range(1, count)
            .Select(i => new Dictionary<string, object> { ["id"] = i, ["name"] = $"item {i}" })
            .ToList()
    };

    private IRenderedComponent<QueryResultTable> Render(QueryResult result, RowSelectionState? selection = null, string? filter = null) =>
        RenderComponent<QueryResultTable>(p =>
        {
            p.Add(x => x.Result, result)
                .Add(x => x.QueryName, "Query1")
                .Add(x => x.SearchFilter, filter);
            if (selection != null)
                p.Add(x => x.SelectionState, selection);
        });

    private static List<string> ShownIds(IRenderedComponent<QueryResultTable> cut) =>
        cut.FindAll("tbody tr.mud-table-row")
            .Select(tr => tr.QuerySelectorAll("td")[1].TextContent.Trim())
            .ToList();

    [Fact]
    public void LargeResult_ShowsTheRowLimitAndSaysHowManyWereFetched()
    {
        // Arrange
        _settings.RowLimit = 2;

        // Act
        var cut = Render(Numbered(5));

        // Assert
        ShownIds(cut).ShouldBe(["1", "2"]);
        cut.Find(".result-limit-bar").TextContent.ShouldContain("Showing 2 of 5 rows");
    }

    [Fact]
    public async Task LoadMore_ShowsTheNextBatchOfRows()
    {
        // Arrange
        _settings.RowLimit = 2;
        var cut = Render(Numbered(5));

        // Act
        await cut.Find(".result-load-more").ClickAsync(new());

        // Assert
        ShownIds(cut).ShouldBe(["1", "2", "3", "4"]);
        cut.Find(".result-limit-bar").TextContent.ShouldContain("Showing 4 of 5 rows");
    }

    [Fact]
    public async Task ShowAll_ShowsEveryRowAndHidesTheBar()
    {
        // Arrange
        _settings.RowLimit = 2;
        var cut = Render(Numbered(5));

        // Act
        await cut.Find(".result-show-all").ClickAsync(new());

        // Assert
        ShownIds(cut).Count.ShouldBe(5);
        cut.FindAll(".result-limit-bar").ShouldBeEmpty();
    }

    [Fact]
    public void ResultWithinTheLimit_ShowsNoLimitBar()
    {
        // Arrange
        _settings.RowLimit = 5;

        // Act
        var cut = Render(Numbered(5));

        // Assert
        ShownIds(cut).Count.ShouldBe(5);
        cut.FindAll(".result-limit-bar").ShouldBeEmpty();
    }

    [Fact]
    public void NewResult_StartsAgainFromTheRowLimit()
    {
        // Arrange
        _settings.RowLimit = 2;
        var cut = Render(Numbered(5));
        cut.Find(".result-show-all").Click();

        // Act
        cut.SetParametersAndRender(p => p.Add(x => x.Result, Numbered(6)));

        // Assert
        ShownIds(cut).Count.ShouldBe(2);
        cut.Find(".result-limit-bar").TextContent.ShouldContain("Showing 2 of 6 rows");
    }

    [Fact]
    public async Task ClickingACell_SelectsThatRowByItsPlaceInTheResult()
    {
        // Arrange
        var selection = new RowSelectionState();
        var cut = Render(Numbered(3), selection);

        // Act
        await cut.FindAll("tbody tr.mud-table-row")[2].QuerySelector(".cell-content")!.ClickAsync(new());

        // Assert
        selection.SelectedIndices.ShouldBe([2]);
    }

    [Fact]
    public void Filter_ShowsOnlyMatchingRows_AndTheLimitCountsMatchingRows()
    {
        // Arrange
        _settings.RowLimit = 1;

        // Act
        var cut = Render(Numbered(12), filter: "item 1");

        // Assert
        ShownIds(cut).ShouldBe(["1"]);
        cut.Find(".result-limit-bar").TextContent.ShouldContain("Showing 1 of 4 matching rows");
    }

    private static QueryResult Revenue() => new()
    {
        Columns = ["customer", "orders", "revenue"],
        Rows =
        [
            new Dictionary<string, object> { ["customer"] = "Charlie", ["orders"] = 2L, ["revenue"] = 259.96999999999997 },
            new Dictionary<string, object> { ["customer"] = "Alice", ["orders"] = 3L, ["revenue"] = null! }
        ]
    };

    private static IElement Cell(IRenderedComponent<QueryResultTable> cut, int row, int column) =>
        cut.FindAll("tbody tr.mud-table-row")[row].QuerySelectorAll("td")[column + 1];

    [Fact]
    public void FloatingPointCell_ShowsTheRoundedValue_WithEveryDigitInItsTitle()
    {
        var cut = Render(Revenue());

        var value = Cell(cut, 0, 2).QuerySelector(".cell-value")!;
        value.TextContent.ShouldBe("259.97");
        value.GetAttribute("title").ShouldBe(259.96999999999997.ToString());
    }

    [Fact]
    public void CellThatNeedsNoRounding_HasNoTitle()
    {
        var cut = Render(Revenue());

        Cell(cut, 0, 1).QuerySelector(".cell-value")!.HasAttribute("title").ShouldBeFalse();
    }

    [Fact]
    public void NullCell_SaysNull()
    {
        var cut = Render(Revenue());

        Cell(cut, 1, 2).QuerySelector(".cell-null")!.TextContent.ShouldBe("NULL");
    }

    [Fact]
    public void NumberColumns_AreRightAligned_AndTextColumnsAreNot()
    {
        var cut = Render(Revenue());

        Cell(cut, 0, 1).QuerySelector(".cell-content")!.ClassList.ShouldContain("numeric");
        Cell(cut, 0, 2).QuerySelector(".cell-content")!.ClassList.ShouldContain("numeric");
        Cell(cut, 0, 0).QuerySelector(".cell-content")!.ClassList.ShouldNotContain("numeric");
    }
}
