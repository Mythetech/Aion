using Aion.Components;
using Aion.Components.Querying;
using Aion.Components.RequestContextPanel;
using Aion.Components.RequestContextPanel.Commands;
using Aion.Components.Shortcuts;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;
using ContextPanel = Aion.Components.RequestContextPanel.RequestContextPanel;
using Index = Aion.Components.Index;

namespace Aion.Test.Components;

public class SidePanelTests : TestContext
{
    public SidePanelTests()
    {
        Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./_content/Aion.Components/js/layout.js");

        var bus = Substitute.For<IMessageBus>();
        Services.AddSingleton(bus);
        Services.AddSingleton(new GlobalAppState());
        Services.AddSingleton(new QueryState(bus, Substitute.For<IQuerySaveService>()));

        Services.AddSingleton(AionKeyBindings.ForDesktop(isMac: false));

        ComponentFactories.AddStub<QueryEditor>();
        ComponentFactories.AddStub<QueryResponsePanel>();
        ComponentFactories.AddStub<ContextPanel>();
        ComponentFactories.AddStub<QueryAutoSaver>();
        ComponentFactories.AddStub<ChordHotkey>();
    }

    private static SidePanelRequest? RequestShown(IRenderedComponent<Index> cut) =>
        cut.FindComponent<Stub<ContextPanel>>().Instance.Parameters.Get(p => p.Request);

    [Fact]
    public async Task OpenCommand_WhileThePanelIsClosed_OpensItOnTheRequestedView()
    {
        var cut = RenderComponent<Index>();
        cut.FindComponents<Stub<ContextPanel>>().ShouldBeEmpty();

        await cut.InvokeAsync(() => cut.Instance.Consume(new OpenRequestContextPanel("Transactions", "")));

        RequestShown(cut)!.View.ShouldBe(SidePanelRequest.TransactionsView);
    }

    [Fact]
    public async Task JsonDetail_WhileThePanelIsClosed_ReachesThePanel()
    {
        var detail = new QueryResponseJsonDetail("Orders", "payload", "{\"a\":1}");
        var cut = RenderComponent<Index>();

        await cut.InvokeAsync(() => cut.Instance.Consume(new OpenJsonDetailView(detail)));

        var request = RequestShown(cut)!;
        request.View.ShouldBe(SidePanelRequest.JsonView);
        request.JsonDetail.ShouldBe(detail);
    }

    [Fact]
    public async Task ForeignKey_WhileThePanelIsClosed_ReachesThePanel()
    {
        var detail = new ForeignKeyDetail("Orders", "customer_id", "customers", "id", 7, Guid.NewGuid(), "shop");
        var cut = RenderComponent<Index>();

        await cut.InvokeAsync(() => cut.Instance.Consume(new OpenForeignKeyView(detail)));

        var request = RequestShown(cut)!;
        request.View.ShouldBe(SidePanelRequest.ForeignKeyView);
        request.ForeignKey.ShouldBe(detail);
    }

    [Fact]
    public async Task SecondOpenCommand_WhileThePanelIsOpen_IsANewRequest()
    {
        var cut = RenderComponent<Index>();
        await cut.InvokeAsync(() => cut.Instance.Consume(new OpenRequestContextPanel("Info", "")));
        var first = RequestShown(cut);

        await cut.InvokeAsync(() => cut.Instance.Consume(new OpenRequestContextPanel("Info", "")));

        RequestShown(cut).ShouldNotBeSameAs(first);
    }
}
