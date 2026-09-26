using Aion.Components.CommandPalette.Commands;
using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Events;
using Aion.Components.Settings.Domains;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using BlazorMonaco;
using BlazorMonaco.Editor;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Interop;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryEditorRunTests : TestContext
{
    private const string Full = "SELECT 1;\nSELECT name FROM products";
    private const string Selected = "SELECT name FROM products";

    private readonly QueryState _state;
    private readonly IMessageBus _bus;
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly QueryModel _query;
    private readonly List<QueryExecuted> _executed = [];
    private readonly TaskCompletionSource<QueryResult> _providerResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _providerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConnectionModel _connection = new() { Name = "Local", Type = DatabaseType.WasmSQLite, ConnectionString = "Data Source=shop.db" };
    private string? _textDuringRun;
    private CancellationToken _runToken;
    private int _runsStarted;
    private readonly TaskCompletionSource _secondRunStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _runCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QueryEditorRunTests()
    {
        Services.AddLogging();
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<BoundingClientRect[]>("mudResizeObserver.connect", _ => true);
        JSInterop.Setup<TextModel>("blazorMonaco.editor.getInstanceModel", _ => true)
            .SetResult(new TextModel { Uri = "inmemory://model/1" });

        var guard = Substitute.For<IJsGuardService>();
        guard.IsReady("monaco").Returns(true);
        guard.WaitForReadyAsync(Arg.Any<Microsoft.JSInterop.IJSRuntime>(), "monaco", Arg.Any<TimeSpan?>()).Returns(true);

        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.WasmSQLite).Returns(_provider);
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db");
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _runToken = call.Arg<CancellationToken>();
                _runToken.Register(() => _runCancelled.TrySetResult());
                if (Interlocked.Increment(ref _runsStarted) == 2) _secondRunStarted.TrySetResult();
                _textDuringRun = _query!.Query;
                _providerStarted.TrySetResult();
                return _providerResult.Task;
            });

        _bus = new InMemoryMessageBus(Services, new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(), Enumerable.Empty<IConsumerFilter>());
        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, _bus, new NullLogger<ConnectionState>())
        {
            Connections = [_connection]
        };
        _bus.Subscribe<QueryChanged>(_state);
        _bus.Subscribe(new RecordingConsumer(_executed));

        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton(guard);
        Services.AddSingleton(new SqlCompletionService(connections));
        Services.AddSingleton<EditorSettings>();
        Services.AddSingleton(Aion.Components.Shortcuts.AionKeyBindings.ForDesktop(isMac: false));

        _query = _state.Queries[0];
        _query.ConnectionId = _connection.Id;
        _query.DatabaseName = "shop";
        _query.Query = Full;
        _state.SetActive(_query);

        JSInterop.Setup<string>("blazorMonaco.editor.getValue", _ => true).SetResult(Full);
        JSInterop.Setup<Selection>("blazorMonaco.editor.getSelection", _ => true)
            .SetResult(new Selection { StartLineNumber = 2, StartColumn = 1, EndLineNumber = 2, EndColumn = 26 });
        JSInterop.Setup<string>("blazorMonaco.editor.model.getValueInRange", _ => true).SetResult(Selected);
    }

    private sealed class RecordingConsumer(List<QueryExecuted> executed) : IConsumer<QueryExecuted>
    {
        public Task Consume(QueryExecuted message)
        {
            executed.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunningASelection_ExecutesTheSelectionWithoutPuttingItInTheTab()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);

        // Act
        var run = cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _providerResult.SetResult(new QueryResult());
        await run;

        // Assert
        await _provider.Received(1).ExecuteQueryAsync("db", Selected, Arg.Any<CancellationToken>());
        _textDuringRun.ShouldBe(Full);
        _query.Query.ShouldBe(Full);
    }

    [Fact]
    public async Task TextTypedWhileARunIsInProgress_IsKeptWhenTheRunFinishes()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);
        var run = cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        const string typed = Full + "\nWHERE price > 10";
        _state.EditQueryText(_query, typed);
        _providerResult.SetResult(new QueryResult());
        await run;

        // Assert
        _query.Query.ShouldBe(typed);
    }

    private void EditorShows(string text) =>
        JSInterop.Setup<string>("blazorMonaco.editor.getValue", _ => true).SetResult(text);

    private IEnumerable<ActionDescriptor> EditorActions() =>
        JSInterop.Invocations["blazorMonaco.editor.addAction"].Select(i => i.Arguments[1]).OfType<ActionDescriptor>();

    // The editor raises OnDidInit once it is created, which registers the editor actions.
    private StandaloneCodeEditor InitializedEditor(IRenderedComponent<QueryEditor> cut)
    {
        cut.WaitForAssertion(() => EditorActions().ShouldContain(a => a.Id == "aion.run-query"));
        return cut.FindComponent<StandaloneCodeEditor>().Instance;
    }

    [Fact]
    public async Task RunShortcutInTheEditor_RunsTheQuery()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        var editor = InitializedEditor(cut);

        // Act
        await cut.InvokeAsync(() => editor.ActionCallback("aion.run-query"));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _providerResult.SetResult(new QueryResult());

        // Assert
        await _provider.Received(1).ExecuteQueryAsync("db", Selected, Arg.Any<CancellationToken>());
        EditorActions().Single(a => a.Id == "aion.run-query").Keybindings
            .ShouldBe([(int)KeyMod.CtrlCmd | (int)KeyCode.Enter]);
    }

    [Fact]
    public async Task RunShortcutWhileTheTabRuns_CancelsTheRun()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        var editor = InitializedEditor(cut);
        await cut.InvokeAsync(() => editor.ActionCallback("aion.run-query"));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        await cut.InvokeAsync(() => editor.ActionCallback("aion.run-query"));

        // Assert
        await _runCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _providerResult.SetCanceled();
        cut.WaitForAssertion(() => _query.IsExecuting.ShouldBeFalse());
        await _provider.Received(1).ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunShortcutInAnotherTab_StartsWhileTheFirstTabStillRuns()
    {
        // Arrange
        var other = _state.AddQuery("Other");
        _state.UpdateQueryConnection(other, _connection);
        _state.UpdateQueryDatabase(other, "shop");
        var cut = RenderComponent<QueryEditor>();
        var editor = InitializedEditor(cut);
        await cut.InvokeAsync(() => _bus.PublishAsync(new FocusQuery(_query)));
        await cut.InvokeAsync(() => editor.ActionCallback("aion.run-query"));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new FocusQuery(other)));
        await cut.InvokeAsync(() => editor.ActionCallback("aion.run-query"));

        // Assert
        await _secondRunStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _query.IsExecuting.ShouldBeTrue();
        _providerResult.SetResult(new QueryResult());
        cut.WaitForAssertion(() => other.IsExecuting.ShouldBeFalse());
        _query.IsExecuting.ShouldBeFalse();
    }

    [Fact]
    public async Task ActiveTabChangedOutsideTheEditor_IsShownInTheEditor()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);

        // Act
        var added = await cut.InvokeAsync(() => _state.AddQuery("Added elsewhere"));

        // Assert
        cut.WaitForAssertion(() =>
            JSInterop.Invocations["blazorMonaco.editor.setValue"].Last().Arguments[1].ShouldBe(added.Query));
        _query.Query.ShouldBe(Full);
    }

    [Fact]
    public async Task PaletteShortcutInTheEditor_OpensTheCommandPalette()
    {
        // Arrange
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _bus.Subscribe(new PaletteRecorder(opened));
        var cut = RenderComponent<QueryEditor>();
        var editor = InitializedEditor(cut);

        // Act
        await cut.InvokeAsync(() => editor.ActionCallback("aion.command-palette"));

        // Assert
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(5));
        EditorActions().Single(a => a.Id == "aion.command-palette").Keybindings.ShouldBe(
        [
            (int)KeyMod.CtrlCmd | (int)KeyCode.KeyK,
            (int)KeyMod.CtrlCmd | (int)KeyMod.Shift | (int)KeyCode.KeyP
        ]);
    }

    private sealed class PaletteRecorder(TaskCompletionSource opened) : IConsumer<OpenCommandPalette>
    {
        public Task Consume(OpenCommandPalette message)
        {
            opened.TrySetResult();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunWithoutADatabase_SaysWhyInsteadOfRunning()
    {
        // Arrange
        var notifications = new List<AddNotification>();
        _bus.Subscribe(new NotificationRecorder(notifications));
        _state.UpdateQueryDatabase(_query, "");
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));

        // Assert
        await _provider.DidNotReceiveWithAnyArgs().ExecuteQueryAsync(default!, default!, default);
        notifications.ShouldHaveSingleItem().Message.ShouldBe("Choose a database");
    }

    private sealed class NotificationRecorder(List<AddNotification> notifications) : IConsumer<AddNotification>
    {
        public Task Consume(AddNotification message)
        {
            notifications.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SwitchingTabs_KeepsTypingTheOutgoingTabHadNotReceivedYet()
    {
        // Arrange
        var other = _state.AddQuery("Other");
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);
        EditorShows(other.Query);
        await cut.InvokeAsync(() => _bus.PublishAsync(new FocusQuery(_query)));
        EditorShows(Full + " LIMIT 5");

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new FocusQuery(other)));

        // Assert
        _query.Query.ShouldBe(Full + " LIMIT 5");
        other.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunningASelection_RecordsTheSelectionAsTheExecutedSql()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        InitializedEditor(cut);

        // Act
        var run = cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _providerResult.SetResult(new QueryResult());
        await run;

        // Assert
        _executed.ShouldHaveSingleItem().ExecutedSql.ShouldBe(Selected);
    }
}
