using Aion.Components.Querying.Results;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class ResultGridViewTests
{
    private static IReadOnlyList<ResultRow> Rows(params object?[] names) =>
        ResultRow.Wrap(names.Select(n => new Dictionary<string, object> { ["name"] = n! }).ToList());

    [Fact]
    public void WithoutAFilter_KeepsEveryRowInFetchedOrder()
    {
        var view = ResultGridView.Build(Rows("b", "a", "c"), filter: null);

        view.Rows.Select(r => r.Index).ShouldBe([0, 1, 2]);
        view.Count.ShouldBe(3);
        view.IsFiltered.ShouldBeFalse();
    }

    [Fact]
    public void Filter_KeepsMatchingRowsWithTheirPlaceInTheResult()
    {
        var view = ResultGridView.Build(Rows("Widget", "bolt", "WIDGET stand", null), filter: "widget");

        view.Rows.Select(r => r.Index).ShouldBe([0, 2]);
        view.IsFiltered.ShouldBeTrue();
    }

    [Fact]
    public void Filter_MatchesAnyColumn()
    {
        var rows = ResultRow.Wrap([
            new Dictionary<string, object> { ["id"] = 7, ["name"] = "bolt" },
            new Dictionary<string, object> { ["id"] = 8, ["name"] = "nut" }
        ]);

        ResultGridView.Build(rows, filter: "8").Rows.Select(r => r.Index).ShouldBe([1]);
    }

    [Fact]
    public void Filter_MatchesTheRoundedNumberTheGridShows()
    {
        var rows = ResultRow.Wrap([
            new Dictionary<string, object> { ["revenue"] = 259.96999999999997 },
            new Dictionary<string, object> { ["revenue"] = 12.5 }
        ]);

        ResultGridView.Build(rows, filter: "259.97").Rows.Select(r => r.Index).ShouldBe([0]);
        ResultGridView.Build(rows, filter: "259.969").Rows.Select(r => r.Index).ShouldBe([0]);
    }

    [Fact]
    public void Take_ReturnsAtMostTheLimit()
    {
        var view = ResultGridView.Build(Rows("a", "b", "c"), filter: null);

        view.Take(2).Select(r => r.Index).ShouldBe([0, 1]);
        view.Take(10).Count.ShouldBe(3);
    }

    private static IReadOnlyList<ResultRow> Values(params object?[] values) =>
        ResultRow.Wrap(values.Select(v => new Dictionary<string, object> { ["v"] = v! }).ToList());

    private static List<object?> Sorted(IReadOnlyList<ResultRow> rows, ResultSortDirection direction) =>
        ResultGridView.Build(rows, filter: null, new ResultSort("v", direction)).Rows.Select(r => (object?)r.Values["v"]).ToList();

    [Fact]
    public void Sort_ComparesNumbersAsNumbers()
    {
        Sorted(Values(10L, 9L, 100L, 2.5), ResultSortDirection.Ascending).ShouldBe([2.5, 9L, 10L, 100L]);
        Sorted(Values(10L, 9L, 100L, 2.5), ResultSortDirection.Descending).ShouldBe([100L, 10L, 9L, 2.5]);
    }

    [Fact]
    public void Sort_ComparesNumberTextAsNumbers()
    {
        Sorted(Values("10", "9", "100"), ResultSortDirection.Ascending).ShouldBe(["9", "10", "100"]);
    }

    [Fact]
    public void Sort_KeepsDecimalsExact()
    {
        Sorted(Values(0.30000000000000004m, 0.3m, 0.30000000000000001m), ResultSortDirection.Ascending)
            .ShouldBe([0.3m, 0.30000000000000001m, 0.30000000000000004m]);
    }

    [Fact]
    public void Sort_ComparesTextIgnoringCase()
    {
        Sorted(Values("banana", "Apple", "cherry"), ResultSortDirection.Ascending).ShouldBe(["Apple", "banana", "cherry"]);
    }

    [Theory]
    [InlineData(ResultSortDirection.Ascending)]
    [InlineData(ResultSortDirection.Descending)]
    public void Sort_PutsNullsLastEitherWay(ResultSortDirection direction)
    {
        Sorted(Values(null, 2L, null, 1L), direction).TakeLast(2).ShouldBe([null, null]);
    }

    [Fact]
    public void Sort_PutsNumbersBeforeText_LikeSqlite()
    {
        Sorted(Values("b", 2L, "a", 1L), ResultSortDirection.Ascending).ShouldBe([1L, 2L, "a", "b"]);
    }

    [Fact]
    public void Sort_ComparesDatesAndBooleansByValue()
    {
        Sorted(Values(new DateTime(2024, 10, 1), new DateTime(2024, 2, 1)), ResultSortDirection.Ascending)
            .ShouldBe([new DateTime(2024, 2, 1), new DateTime(2024, 10, 1)]);
        Sorted(Values(true, false), ResultSortDirection.Ascending).ShouldBe([false, true]);
    }

    [Fact]
    public void Sort_KeepsFetchedOrderForEqualValues()
    {
        var rows = ResultRow.Wrap([
            new Dictionary<string, object> { ["v"] = 1L, ["n"] = "first" },
            new Dictionary<string, object> { ["v"] = 0L, ["n"] = "zero" },
            new Dictionary<string, object> { ["v"] = 1L, ["n"] = "second" }
        ]);

        ResultGridView.Build(rows, filter: null, new ResultSort("v", ResultSortDirection.Descending))
            .Rows.Select(r => r.Values["n"]).ShouldBe(["first", "second", "zero"]);
    }

    [Fact]
    public void Sort_AppliesToTheFilteredRows()
    {
        var view = ResultGridView.Build(Values("b1", "a1", "c2"), filter: "1", new ResultSort("v", ResultSortDirection.Ascending));

        view.Rows.Select(r => r.Values["v"]).ShouldBe(["a1", "b1"]);
    }

    [Fact]
    public void NextSort_CyclesAscendingDescendingThenNone()
    {
        var ascending = ResultSort.Next(null, "price");
        ascending.ShouldBe(new ResultSort("price", ResultSortDirection.Ascending));

        var descending = ResultSort.Next(ascending, "price");
        descending.ShouldBe(new ResultSort("price", ResultSortDirection.Descending));

        ResultSort.Next(descending, "price").ShouldBeNull();
        ResultSort.Next(descending, "name").ShouldBe(new ResultSort("name", ResultSortDirection.Ascending));
    }
}
