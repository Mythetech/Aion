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
    public void Take_ReturnsAtMostTheLimit()
    {
        var view = ResultGridView.Build(Rows("a", "b", "c"), filter: null);

        view.Take(2).Select(r => r.Index).ShouldBe([0, 1]);
        view.Take(10).Count.ShouldBe(3);
    }
}
