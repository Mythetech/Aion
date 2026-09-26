using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Components.Settings.Domains;
using Aion.Components.Shortcuts;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using AngleSharp.Dom;
using BlazorMonaco;
using BlazorMonaco.Editor;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Interop;
using MudBlazor.Services;
using Mythetech.Framework.Components.Kbd;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryEditorRunMenuTests : TestContext
{
    private const string Full = "SELECT 1;\nSELECT name FROM products";
    private const string Selected = "SELECT name FROM products";

    private static readonly Selection Caret = new() { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 1 };
    private static readonly Selection SecondLine = new() { StartLineNumber = 2, StartColumn = 1, EndLineNumber = 2, EndColumn = 26 };

    private readonly QueryState _state;
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, IEstimatedQueryPlanProvider, IActualQueryPlanProvider>();
    private readonly ConnectionModel _connection = new() { Name = "Local", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=localhost" };
    private readonly QueryModel _query;
    private readonly IRenderedComponent<MudPopoverProvider> _popovers;
    private AionKeyBindings _keys = AionKeyBindings.ForDesktop(isMac: false);

    public QueryEditorRunMenuTests()
    {
        Services.AddLogging();
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<BoundingClientRect[]>("mudResizeObserver.connect", _ => true);
        JSInterop.Setup<TextModel>("blazorMonaco.editor.getInstanceModel", _ => true)
            .SetResult(new TextModel { Uri = "inmemory://model/1" });
        JSInterop.Setup<string>("blazorMonaco.editor.getValue", _ => true).SetResult(Full);
        JSInterop.Setup<string>("blazorMonaco.editor.model.getValueInRange", _ => true).SetResult(Selected);
        EditorSelectionIs(Caret);

        var guard = Substitute.For<IJsGuardService>();
        guard.IsReady("monaco").Returns(true);
        guard.WaitForReadyAsync(Arg.Any<Microsoft.JSInterop.IJSRuntime>(), "monaco", Arg.Any<TimeSpan?>()).Returns(true);

        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db");
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new QueryResult());
        ((IEstimatedQueryPlanProvider)_provider)
            .GetEstimatedPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryPlan { PlanType = "Estimated", PlanFormat = "TEXT", PlanContent = "Seq Scan" });
        ((IActualQueryPlanProvider)_provider)
            .GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryPlan { PlanType = "Actual", PlanFormat = "TEXT", PlanContent = "Seq Scan (actual rows=3)" });

        var bus = new InMemoryMessageBus(Services, new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(), Enumerable.Empty<IConsumerFilter>());
        _state = new QueryState(bus, Substitute.For<IQuerySaveService>());
        var connections = new ConnectionState(Substitute.For<IConnectionService>(), _factory, bus, new NullLogger<ConnectionState>())
        {
            Connections = [_connection]
        };
        bus.Subscribe<QueryChanged>(_state);

        Services.AddSingleton<IMessageBus>(bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton(guard);
        Services.AddSingleton(new SqlCompletionService(connections));
        Services.AddSingleton<EditorSettings>();
        Services.AddSingleton(Substitute.For<IPlatformDetector>());
        // Resolved when the editor first renders, so a test can still pick another platform's keys.
        Services.AddSingleton(_ => _keys);

        _query = _state.Queries[0];
        _query.ConnectionId = _connection.Id;
        _query.DatabaseName = "shop";
        _query.Query = Full;
        _state.SetActive(_query);

        _popovers = RenderComponent<MudPopoverProvider>();
    }

    private void EditorSelectionIs(Selection selection) =>
        JSInterop.Setup<Selection>("blazorMonaco.editor.getSelection", _ => true).SetResult(selection);

    private IRenderedComponent<QueryEditor> RenderEditor()
    {
        var cut = RenderComponent<QueryEditor>();
        cut.WaitForAssertion(() => JSInterop.Invocations["blazorMonaco.editor.addAction"].ShouldNotBeEmpty());
        return cut;
    }

    private static async Task SelectInEditorAsync(IRenderedComponent<QueryEditor> cut, Selection selection)
    {
        var editor = cut.FindComponent<StandaloneCodeEditor>().Instance;
        await cut.InvokeAsync(() => editor.OnDidChangeCursorSelection.InvokeAsync(new CursorSelectionChangedEvent { Selection = selection }));
    }

    private static async Task OpenRunOptionsAsync(IRenderedComponent<QueryEditor> cut) =>
        await cut.Find(".run-options button").ClickAsync(new MouseEventArgs());

    private IElement? RunOption(string title) =>
        _popovers.FindAll(".mud-menu-item").FirstOrDefault(item => item.QuerySelector(".run-option-title")?.TextContent == title);

    [Fact]
    public void Run_IsTheFirstControlInTheToolbar()
    {
        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".editor-toolbar").QuerySelectorAll("button").First().ClassList.ShouldContain("run-query-button");
    }

    [Fact]
    public void Run_ShowsItsShortcut()
    {
        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".run-query-button kbd").TextContent.ShouldBe("Ctrl+Enter");
    }

    [Fact]
    public void Run_OnAMac_ShowsTheMacShortcut()
    {
        // Arrange
        _keys = AionKeyBindings.ForBrowser(isMac: true);

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".run-query-button kbd").TextContent.ShouldBe("⌘↵");
    }

    [Fact]
    public async Task RunSelection_IsOfferedOnlyWhileTheEditorHasASelection()
    {
        // Arrange
        var cut = RenderEditor();

        // Act
        await OpenRunOptionsAsync(cut);
        var withoutSelection = RunOption("Run selection")!.GetAttribute("aria-disabled");
        await SelectInEditorAsync(cut, SecondLine);

        // Assert
        withoutSelection.ShouldBe("true");
        RunOption("Run selection")!.GetAttribute("aria-disabled").ShouldBe("false");
    }

    [Fact]
    public async Task RunSelection_RunsOnlyTheSelectedSql()
    {
        // Arrange
        var cut = RenderEditor();
        EditorSelectionIs(SecondLine);
        await SelectInEditorAsync(cut, SecondLine);
        await OpenRunOptionsAsync(cut);

        // Act
        await RunOption("Run selection")!.ClickAsync(new MouseEventArgs());

        // Assert
        cut.WaitForAssertion(() => _query.IsExecuting.ShouldBeFalse());
        await _provider.Received(1).ExecuteQueryAsync("db", Selected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explain_ShowsTheEstimatedPlanWithoutRunningTheStatement()
    {
        // Arrange
        var cut = RenderEditor();
        await OpenRunOptionsAsync(cut);

        // Act
        await RunOption("Explain")!.ClickAsync(new MouseEventArgs());

        // Assert
        cut.WaitForAssertion(() => _query.EstimatedPlan.ShouldNotBeNull());
        await ((IEstimatedQueryPlanProvider)_provider).Received(1).GetEstimatedPlanAsync("db", Full, Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _query.ResultKind.ShouldBe(QueryResultKind.EstimatedPlan);
        cut.FindAll(".mud-alert").ShouldBeEmpty();
    }

    [Fact]
    public async Task ExplainAnalyze_CapturesTheActualPlanOnceWithoutTurningTheToggleOn()
    {
        // Arrange
        var cut = RenderEditor();
        await OpenRunOptionsAsync(cut);

        // Act
        await RunOption("Explain analyze")!.ClickAsync(new MouseEventArgs());

        // Assert
        cut.WaitForAssertion(() => _query.ActualPlan.ShouldNotBeNull());
        _query.IncludeActualPlan.ShouldBeFalse();
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExplainOptions_FollowWhatTheEngineCanPlan()
    {
        // Arrange
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(Substitute.For<IDatabaseProvider, IEstimatedQueryPlanProvider>());
        var cut = RenderComponent<QueryEditor>();

        // Act
        await OpenRunOptionsAsync(cut);

        // Assert
        RunOption("Explain").ShouldNotBeNull();
        RunOption("Explain analyze").ShouldBeNull();
    }

    [Fact]
    public async Task ExplainAnalyze_WhileATransactionIsOpen_IsUnavailableAndSaysWhy()
    {
        // Arrange
        _query.UseTransaction = true;
        _query.Transaction = new TransactionInfo();
        var cut = RenderComponent<QueryEditor>();

        // Act
        await OpenRunOptionsAsync(cut);

        // Assert
        var option = RunOption("Explain analyze")!;
        option.GetAttribute("aria-disabled").ShouldBe("true");
        option.TextContent.ShouldContain("Commit or roll back first");
    }

    [Fact]
    public void RunOptions_WithoutADatabase_AreUnavailable()
    {
        // Arrange
        _query.DatabaseName = null;

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".run-options button").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void RunOptions_WhileTheQueryRuns_AreUnavailable()
    {
        // Arrange
        _query.IsExecuting = true;

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".cancel-query-button").TextContent.ShouldContain("Cancel");
        cut.Find(".run-options button").HasAttribute("disabled").ShouldBeTrue();
    }
}
