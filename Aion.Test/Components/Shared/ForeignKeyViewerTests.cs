using Aion.Components;
using Aion.Components.ForeignKeys;
using Aion.Components.ForeignKeys.Commands;
using Aion.Components.Querying;
using Aion.Components.RequestContextPanel;
using Aion.Components.Shared;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;
using ContextPanel = Aion.Components.RequestContextPanel.RequestContextPanel;

namespace Aion.Test.Components.Shared;

public class ForeignKeyViewerTests : TestContext
{
    private readonly IForeignKeyService _foreignKeys = Substitute.For<IForeignKeyService>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private static readonly Guid ConnectionId = Guid.NewGuid();

    public ForeignKeyViewerTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_foreignKeys);
        Services.AddSingleton(_bus);
        Services.AddSingleton(new GlobalAppState());
        Services.AddSingleton(new QueryState(_bus, Substitute.For<IQuerySaveService>()));
    }

    private static ForeignKeyDetail Customer(object id) =>
        new("Orders", "customer_id", "customers", "id", id, ConnectionId, "shop");

    private static ForeignKeyLookup Row(string name) => new(new Dictionary<string, object> { ["name"] = name }, null);

    private void Returns(object id, ForeignKeyLookup lookup) =>
        _foreignKeys.FetchReferencedRowAsync(Arg.Is<ForeignKeyDetail>(d => Equals(d.ForeignKeyValue, id)), Arg.Any<CancellationToken>())
            .Returns(lookup);

    private static string ViewerJson(IRenderedComponent<ForeignKeyViewer> cut) =>
        cut.FindComponents<JsonViewer>().SingleOrDefault()?.Instance.Json ?? "";

    private IRenderedComponent<ForeignKeyViewer> RenderViewer(ForeignKeyDetail detail) =>
        RenderComponent<ForeignKeyViewer>(p => p.Add(x => x.Detail, detail));

    [Fact]
    public void SameColumnWithANewValue_LooksUpTheNewRow()
    {
        Returns(1, Row("Ada"));
        Returns(2, Row("Grace"));
        var cut = RenderViewer(Customer(1));
        cut.WaitForAssertion(() => ViewerJson(cut).ShouldContain("Ada"));

        cut.SetParametersAndRender(p => p.Add(x => x.Detail, Customer(2)));

        cut.WaitForAssertion(() => ViewerJson(cut).ShouldContain("Grace"));
    }

    [Fact]
    public void AnswerForAnEarlierValue_ArrivingLate_DoesNotReplaceTheCurrentRow()
    {
        var slow = new TaskCompletionSource<ForeignKeyLookup>();
        _foreignKeys.FetchReferencedRowAsync(Arg.Is<ForeignKeyDetail>(d => Equals(d.ForeignKeyValue, 1)), Arg.Any<CancellationToken>())
            .Returns(slow.Task);
        Returns(2, Row("Grace"));
        var cut = RenderViewer(Customer(1));

        cut.SetParametersAndRender(p => p.Add(x => x.Detail, Customer(2)));
        cut.WaitForAssertion(() => ViewerJson(cut).ShouldContain("Grace"));
        cut.InvokeAsync(() => slow.SetResult(Row("Ada")));

        ViewerJson(cut).ShouldContain("Grace");
    }

    [Fact]
    public void FailedLookup_ShowsTheEngineError()
    {
        Returns(1, new ForeignKeyLookup(null, "no such table: customers"));

        var cut = RenderViewer(Customer(1));

        cut.WaitForAssertion(() => cut.Find(".fk-viewer-error").TextContent.ShouldContain("no such table: customers"));
    }

    [Fact]
    public async Task ExpandInThePanel_OpensTheRelatedRowsForTheShownKey()
    {
        Returns(7, Row("Ada"));
        var detail = Customer(7);
        var cut = RenderComponent<ContextPanel>(p => p.Add(x => x.Request, new SidePanelRequest
        {
            View = SidePanelRequest.ForeignKeyView,
            ForeignKey = detail
        }));

        await cut.Find("[aria-label='Open related rows in a new tab']").ClickAsync(new());

        await _bus.Received(1).PublishAsync(Arg.Is<OpenForeignKeyRows>(m => m.Detail == detail));
    }
}
