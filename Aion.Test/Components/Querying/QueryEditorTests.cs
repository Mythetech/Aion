using Aion.Core.Database;
using Aion.Components.Connections;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Interop;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.Guards;
using NSubstitute;
using Shouldly;
using Aion.Components.Settings.Domains;

namespace Aion.Test.Components.Querying;

public class QueryEditorTests : TestContext
{
    private QueryState _state;
    private ConnectionState _connections;
    private IMessageBus _bus;

    public QueryEditorTests()
    {
        Services.AddLogging();
        Services.AddMudServices(x =>
        {
            x.PopoverOptions.CheckForPopoverProvider = false;
        });
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        JSInterop.SetupVoid("mudPopover.connect", _ => true);
        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.Setup<BoundingClientRect[]>("mudResizeObserver.connect", _ => true);
        JSInterop.Setup<BoundingClientRect>("mudElementRef.getBoundingClientRect", _ => true);
        JSInterop.SetupVoid("blazorMonaco.editor.setWasm", false);
        JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudDragAndDrop.initDropZone", _ => true);
        JSInterop.SetupVoid("mudDragAndDrop.connect", _ => true);

        _bus = new InMemoryMessageBus(
            this.Services,
            new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(),
            Enumerable.Empty<IConsumerFilter>());
        _state = new(_bus, NSubstitute.Substitute.For<IQuerySaveService>());
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), Substitute.For<IDatabaseProviderFactory>(), _bus, new NullLogger<ConnectionState>());
        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(_connections);
        Services.AddSingleton(Substitute.For<IJsGuardService>());
        Services.AddSingleton(new SqlCompletionService(_connections));
        Services.AddSingleton<EditorSettings>();
    }

    [Fact]
    public void Can_Render_QueryEditor()
    {
        // Arrange & Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.ShouldNotBeNull();
    }

    [Fact]
    public void Can_Render_WithNullActiveQuery()
    {
        // Arrange
        _state.SetActive(null!);

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.ShouldNotBeNull();

    }

    [Fact]
    public void FailedRun_ShowsNoErrorBannerAboveTheEditor()
    {
        // Arrange
        var query = _state.Queries[0];
        _state.SetActive(query);
        query.SetResult(new Aion.Contracts.Queries.QueryResult { Error = "no such column: categry_id" });

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.FindAll(".mud-alert").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("categry_id");
    }

    [Fact]
    public void Run_WithAConnectionButNoDatabase_IsDisabledAndSaysWhy()
    {
        // Arrange
        var connection = new ConnectionModel { Name = "pg", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=pg" };
        _connections.Connections = [connection];
        var query = _state.Queries[0];
        query.ConnectionId = connection.Id;
        _state.SetActive(query);

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        var run = cut.FindComponent<RunQueryButton>().Instance;
        run.Disabled.ShouldBeTrue();
        run.DisabledReason.ShouldBe("Choose a database");
    }

    [Fact]
    public void Run_WithoutAConnection_SaysToChooseOne()
    {
        // Arrange
        _state.SetActive(_state.Queries[0]);

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.FindComponent<RunQueryButton>().Instance.DisabledReason.ShouldBe("Choose a connection");
    }

    [Fact]
    public void Editor_FollowsItsContainerSize()
    {
        // Arrange
        JSInterop.Mode = JSRuntimeMode.Loose;
        var guard = Substitute.For<IJsGuardService>();
        guard.IsReady("monaco").Returns(true);
        guard.WaitForReadyAsync(Arg.Any<Microsoft.JSInterop.IJSRuntime>(), "monaco", Arg.Any<TimeSpan?>()).Returns(true);
        Services.AddSingleton(guard);

        // Act
        var cut = RenderComponent<QueryEditor>();
        var editor = cut.FindComponent<BlazorMonaco.Editor.StandaloneCodeEditor>().Instance;
        var options = editor.ConstructionOptions(editor);

        // Assert
        options.AutomaticLayout.ShouldBe(true);
    }
}