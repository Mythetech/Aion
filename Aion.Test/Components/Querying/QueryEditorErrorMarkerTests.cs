using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Events;
using Aion.Components.Settings.Domains;
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

public class QueryEditorErrorMarkerTests : TestContext
{
    private const string Sql = "SELECT name\nFROM products\nWHERE categry_id = 2";
    private const string UnknownColumn = "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id";

    private readonly QueryState _state;
    private readonly IMessageBus _bus;
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly QueryModel _query;

    public QueryEditorErrorMarkerTests()
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

        _bus = new InMemoryMessageBus(Services, new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(), Enumerable.Empty<IConsumerFilter>());
        _state = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        var connection = new ConnectionModel { Name = "Local", Type = DatabaseType.WasmSQLite, ConnectionString = "Data Source=shop.db" };
        var connections = new ConnectionState(Substitute.For<IConnectionService>(), factory, _bus, new NullLogger<ConnectionState>())
        {
            Connections = [connection]
        };
        _bus.Subscribe<QueryChanged>(_state);

        Services.AddSingleton(_bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton(guard);
        Services.AddSingleton(new SqlCompletionService(connections));
        Services.AddSingleton<EditorSettings>();

        _query = _state.Queries[0];
        _query.ConnectionId = connection.Id;
        _query.DatabaseName = "shop";
        _query.Query = Sql;
        _state.SetActive(_query);
    }

    private void EditorHolds(string text, Selection? selection = null)
    {
        _query.Query = text;
        JSInterop.Setup<string>("blazorMonaco.editor.getValue", _ => true).SetResult(text);
        JSInterop.Setup<Selection>("blazorMonaco.editor.getSelection", _ => true)
            .SetResult(selection ?? new Selection { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 1 });
    }

    private void ProviderFails(string error) =>
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new QueryResult { Error = error });

    private List<MarkerData> LastMarkers()
    {
        var invocation = JSInterop.Invocations["blazorMonaco.editor.setModelMarkers"].Last();
        return (List<MarkerData>)invocation.Arguments[2]!;
    }

    [Fact]
    public async Task FailedRun_MarksTheOffendingTokenInTheEditor()
    {
        // Arrange
        EditorHolds(Sql);
        ProviderFails(UnknownColumn);
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));

        // Assert
        var marker = LastMarkers().ShouldHaveSingleItem();
        marker.Severity.ShouldBe(MarkerSeverity.Error);
        marker.StartLineNumber.ShouldBe(3);
        marker.StartColumn.ShouldBe(7);
        marker.EndColumn.ShouldBe(17);
        marker.Message.ShouldBe("no such column: categry_id");
    }

    [Fact]
    public async Task FailedRunOfASelection_PlacesTheErrorInTheWholeText()
    {
        // Arrange: only the last line is selected and run.
        const string selected = "SELECT categry_id FROM products";
        const string full = "SELECT 1;\n    " + selected;
        EditorHolds(full, new Selection { StartLineNumber = 2, StartColumn = 5, EndLineNumber = 2, EndColumn = 36 });
        JSInterop.Setup<string>("blazorMonaco.editor.model.getValueInRange", _ => true).SetResult(selected);
        ProviderFails(UnknownColumn);
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));

        // Assert
        var error = _query.Result!.ErrorDetail!;
        error.Line.ShouldBe(2);
        error.Column.ShouldBe(12);
        error.EndColumn.ShouldBe(22);
        _query.Query.ShouldBe(full);
        LastMarkers().ShouldHaveSingleItem().StartColumn.ShouldBe(12);
    }

    [Fact]
    public async Task SuccessfulRunAfterAFailure_ClearsTheMarker()
    {
        // Arrange
        EditorHolds(Sql);
        ProviderFails(UnknownColumn);
        var cut = RenderComponent<QueryEditor>();
        await cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new QueryResult());

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new RunQuery()));

        // Assert
        LastMarkers().ShouldBeEmpty();
    }

    [Fact]
    public async Task GoToQueryPosition_MovesTheCursorThereAndFocusesTheEditor()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new GoToQueryPosition(_query.Id, 3, 7)));

        // Assert
        var position = (Position)JSInterop.Invocations["blazorMonaco.editor.setPosition"].Single().Arguments[1]!;
        position.LineNumber.ShouldBe(3);
        position.Column.ShouldBe(7);
        JSInterop.Invocations["blazorMonaco.editor.revealPositionInCenterIfOutsideViewport"].Count.ShouldBe(1);
        JSInterop.Invocations["blazorMonaco.editor.focus"].Count.ShouldBe(1);
    }

    [Fact]
    public async Task GoToQueryPosition_ForAnotherTab_DoesNothing()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.InvokeAsync(() => _bus.PublishAsync(new GoToQueryPosition(Guid.NewGuid(), 3, 7)));

        // Assert
        JSInterop.Invocations["blazorMonaco.editor.setPosition"].ShouldBeEmpty();
    }
}
