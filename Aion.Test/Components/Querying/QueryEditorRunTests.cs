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
    private string? _textDuringRun;

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
            .Returns(_ =>
            {
                _textDuringRun = _query!.Query;
                _providerStarted.TrySetResult();
                return _providerResult.Task;
            });

        _bus = new InMemoryMessageBus(Services, new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(), Enumerable.Empty<IConsumerFilter>());
        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var connection = new ConnectionModel { Name = "Local", Type = DatabaseType.WasmSQLite, ConnectionString = "Data Source=shop.db" };
        var connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, _bus, new NullLogger<ConnectionState>())
        {
            Connections = [connection]
        };
        _bus.Subscribe<QueryChanged>(_state);
        _bus.Subscribe(new RecordingConsumer(_executed));

        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton(guard);
        Services.AddSingleton(new SqlCompletionService(connections));
        Services.AddSingleton<EditorSettings>();

        _query = _state.Queries[0];
        _query.ConnectionId = connection.Id;
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

    [Fact]
    public async Task RunWithoutADatabase_SaysWhyInsteadOfRunning()
    {
        // Arrange
        var notifications = new List<AddNotification>();
        _bus.Subscribe(new NotificationRecorder(notifications));
        _state.UpdateQueryDatabase(_query, "");
        var cut = RenderComponent<QueryEditor>();

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
        await cut.InvokeAsync(() => _bus.PublishAsync(new FocusQuery(_query)));
        JSInterop.Setup<string>("blazorMonaco.editor.getValue", _ => true).SetResult(Full + " LIMIT 5");

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

        // Act
        var run = cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));
        await _providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _providerResult.SetResult(new QueryResult());
        await run;

        // Assert
        _executed.ShouldHaveSingleItem().ExecutedSql.ShouldBe(Selected);
    }
}
