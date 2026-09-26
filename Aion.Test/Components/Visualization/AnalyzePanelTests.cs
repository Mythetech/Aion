using Aion.Components.Visualization;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace Aion.Test.Components.Visualization;

public class AnalyzePanelTests : TestContext
{
    public AnalyzePanelTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_DefaultChart_WithoutAnAnalyzeButton()
    {
        RenderComponent<MudPopoverProvider>();

        var cut = RenderComponent<AnalyzePanel>(p => p.Add(x => x.Result, CustomerRevenue()));

        cut.Find(".chart-title").TextContent.ShouldBe("Revenue by customer");
        cut.Find(".chart-subtitle").TextContent.ShouldBe("Sum of revenue · 4 customers · from current result");
        cut.FindAll(".hbar-row").Count.ShouldBe(4);
        cut.FindAll(".hbar-label").Select(l => l.TextContent).ShouldBe(["Charlie Davis", "Alice Johnson", "George Kim", "Hannah Lee"]);
        cut.FindAll("button").ShouldNotContain(b => b.TextContent.Trim() == "Analyze");
    }

    [Fact]
    public async Task ChangingGroupBy_RedrawsTheChartImmediately()
    {
        var popovers = RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<AnalyzePanel>(p => p.Add(x => x.Result, CustomerRevenue()));

        await cut.Find("[data-field=group-by] .field-pill").ClickAsync(new MouseEventArgs());
        await MenuItem(popovers, "city").ClickAsync(new MouseEventArgs());

        cut.Find(".chart-title").TextContent.ShouldBe("Revenue by city");
        cut.FindAll(".hbar-label").Select(l => l.TextContent).ShouldBe(["Denver", "Austin"]);
        cut.FindAll(".hbar-value").Select(v => v.TextContent).ShouldBe(["424.95", "279.94"]);
    }

    [Fact]
    public async Task ChangingFunction_RedrawsTheChartImmediately()
    {
        var popovers = RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<AnalyzePanel>(p => p.Add(x => x.Result, CustomerRevenue()));

        await cut.Find("[data-field=measure] .field-pill").ClickAsync(new MouseEventArgs());
        await MenuItem(popovers, "Count").ClickAsync(new MouseEventArgs());

        cut.Find(".chart-title").TextContent.ShouldBe("Row count by customer");
        cut.FindAll(".hbar-value").Select(v => v.TextContent).ShouldAllBe(v => v == "1");
    }

    [Fact]
    public async Task TableView_ListsTheAggregatedValues()
    {
        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<AnalyzePanel>(p => p.Add(x => x.Result, CustomerRevenue()));

        await cut.FindAll(".chart-type-toggle .mud-toggle-item")[3].ClickAsync(new MouseEventArgs());

        cut.FindAll(".hbar-row").ShouldBeEmpty();
        cut.FindAll(".aggregation-table tbody tr").Count.ShouldBe(4);
        cut.FindAll(".aggregation-table tbody td.numeric").Select(td => td.TextContent).First().ShouldBe("259.97");
    }

    [Fact]
    public async Task NewResultWithDifferentColumns_PicksNewDefaults()
    {
        RenderComponent<MudPopoverProvider>();
        var cut = RenderComponent<AnalyzePanel>(p => p.Add(x => x.Result, CustomerRevenue()));

        var products = new QueryResult
        {
            Columns = ["id", "name", "price"],
            Rows =
            [
                new() { ["id"] = 1L, ["name"] = "Laptop", ["price"] = 999.99 },
                new() { ["id"] = 2L, ["name"] = "Mouse", ["price"] = 19.99 }
            ]
        };
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(AnalyzePanel.Result)] = products })));

        cut.Find(".chart-title").TextContent.ShouldBe("Price by name");
    }

    private static AngleSharp.Dom.IElement MenuItem(IRenderedFragment popovers, string text)
        => popovers.FindAll(".mud-menu-item").Single(i => i.QuerySelector(".menu-option-text")?.TextContent == text);

    private static QueryResult CustomerRevenue() => new()
    {
        Columns = ["customer", "city", "orders", "revenue"],
        Rows =
        [
            new() { ["customer"] = "Charlie Davis", ["city"] = "Denver", ["orders"] = 3L, ["revenue"] = 259.96999999999997 },
            new() { ["customer"] = "Alice Johnson", ["city"] = "Austin", ["orders"] = 2L, ["revenue"] = 234.95 },
            new() { ["customer"] = "George Kim", ["city"] = "Denver", ["orders"] = 2L, ["revenue"] = 164.98 },
            new() { ["customer"] = "Hannah Lee", ["city"] = "Austin", ["orders"] = 1L, ["revenue"] = 44.99 }
        ]
    };
}
