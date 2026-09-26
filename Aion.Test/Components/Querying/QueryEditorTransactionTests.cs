using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Consumers;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Components.Settings.Domains;
using Aion.Components.Shared;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Interop;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryEditorTransactionTests : TestContext
{
    private readonly QueryState _state;
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, IActualQueryPlanProvider, IEstimatedQueryPlanProvider>();
    private readonly ConnectionModel _connection = new() { Name = "Test", Type = DatabaseType.PostgreSQL, ConnectionString = "Host=localhost" };
    private readonly QueryModel _query;

    public QueryEditorTransactionTests()
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

        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db-connection");
        _provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);

        var bus = new InMemoryMessageBus(
            Services,
            new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(),
            Enumerable.Empty<IConsumerFilter>());
        _state = new QueryState(bus, Substitute.For<IQuerySaveService>());
        var connections = new ConnectionState(Substitute.For<IConnectionService>(), _factory, bus, new NullLogger<ConnectionState>())
        {
            Connections = [_connection]
        };

        bus.Subscribe<QueryChanged>(_state);
        bus.RegisterConsumerType<CommitTransaction, TransactionFinalizer>();
        bus.RegisterConsumerType<RollbackTransaction, TransactionFinalizer>();

        Services.AddSingleton<IMessageBus>(bus);
        Services.AddSingleton(_state);
        Services.AddSingleton(connections);
        Services.AddSingleton<TransactionFinalizer>();
        Services.AddSingleton(Substitute.For<IJsGuardService>());
        Services.AddSingleton(new SqlCompletionService(connections));
        Services.AddSingleton<EditorSettings>();
        Services.AddSingleton(Aion.Components.Shortcuts.AionKeyBindings.ForDesktop(isMac: false));

        _query = _state.Queries[0];
        _query.ConnectionId = _connection.Id;
        _query.DatabaseName = "db";
        _query.Query = "UPDATE accounts SET balance = 0";
        _state.SetActive(_query);
    }

    private void OpenTransaction(int statements = 0)
    {
        var transaction = new TransactionInfo();
        for (var i = 0; i < statements; i++)
        {
            transaction = transaction.WithStatementExecuted();
        }

        _query.UseTransaction = true;
        _query.Transaction = transaction;
    }

    private static IRenderedComponent<ToggleChip>? FindToggle(IRenderedComponent<QueryEditor> cut, string text) =>
        cut.FindComponents<ToggleChip>().SingleOrDefault(c => c.Instance.Text == text);

    [Fact]
    public void RunButton_WithOpenTransactionAndNothingExecuting_OffersRunNotCancel()
    {
        // Arrange
        OpenTransaction();

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        cut.Find(".run-query-button").TextContent.ShouldContain("Run");
        cut.FindAll(".cancel-query-button").ShouldBeEmpty();
    }

    [Fact]
    public void TransactionBar_ShowsStateOnlyWhileTransactionIsOpen()
    {
        // Arrange
        var cut = RenderComponent<QueryEditor>();
        cut.FindAll(".transaction-bar").ShouldBeEmpty();

        // Act
        OpenTransaction(statements: 2);
        cut.Render();

        // Assert
        var bar = cut.Find(".transaction-bar");
        bar.TextContent.ShouldContain("Transaction open");
        bar.TextContent.ShouldContain("2 statements");
        cut.FindAll(".transaction-bar-commit").Count.ShouldBe(1);
        cut.FindAll(".transaction-bar-rollback").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_WithOpenTransaction_ExecutesInsideItAndNeverRollsBack()
    {
        // Arrange
        OpenTransaction(statements: 1);
        var transactionId = _query.Transaction!.Value.Id;
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.Find(".run-query-button").ClickAsync(new MouseEventArgs());

        // Assert
        await _provider.Received(1).ExecuteInTransactionAsync("db-connection", _query.Query, transactionId, Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().RollbackTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
        _query.HasOpenTransaction.ShouldBeTrue();
        cut.Find(".transaction-bar").TextContent.ShouldContain("2 statements");
    }

    [Fact]
    public async Task CommitButton_CommitsOwningTransactionAndHidesBar()
    {
        // Arrange
        OpenTransaction();
        var transactionId = _query.Transaction!.Value.Id;
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.Find(".transaction-bar-commit").ClickAsync(new MouseEventArgs());

        // Assert
        await _provider.Received(1).CommitTransactionAsync("db-connection", transactionId);
        _query.Transaction.ShouldBeNull();
        cut.FindAll(".transaction-bar").ShouldBeEmpty();
    }

    [Fact]
    public async Task RollbackButton_RollsBackOwningTransactionAndHidesBar()
    {
        // Arrange
        OpenTransaction();
        var transactionId = _query.Transaction!.Value.Id;
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.Find(".transaction-bar-rollback").ClickAsync(new MouseEventArgs());

        // Assert
        await _provider.Received(1).RollbackTransactionAsync("db-connection", transactionId);
        await _provider.DidNotReceive().CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
        _query.Transaction.ShouldBeNull();
        cut.FindAll(".transaction-bar").ShouldBeEmpty();
    }

    [Fact]
    public void TransactionsToggle_IsLockedWhileTransactionIsOpen()
    {
        // Arrange
        OpenTransaction();

        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        FindToggle(cut, "Transactions")!.Instance.Disabled.ShouldBeTrue();
    }

    [Fact]
    public void PlanToggles_AreShownWhenProviderSupportsThem()
    {
        // Act
        var cut = RenderComponent<QueryEditor>();

        // Assert
        FindToggle(cut, "Estimated Query Plan").ShouldNotBeNull();
        FindToggle(cut, "Actual Query Plan").ShouldNotBeNull();
        FindToggle(cut, "Transactions")!.Instance.Disabled.ShouldBeFalse();
    }

    [Fact]
    public void PlanToggles_AreHiddenWhenProviderCannotHonourThem()
    {
        // Arrange
        var estimatedOnly = Substitute.For<IDatabaseProvider, IEstimatedQueryPlanProvider>();
        var noPlans = Substitute.For<IDatabaseProvider>();
        _factory.GetProvider(DatabaseType.WasmSQLite).Returns(estimatedOnly);
        _factory.GetProvider(DatabaseType.LiteDB).Returns(noPlans);

        // Act
        _connection.Type = DatabaseType.WasmSQLite;
        var sqlite = RenderComponent<QueryEditor>();
        _connection.Type = DatabaseType.LiteDB;
        var liteDb = RenderComponent<QueryEditor>();

        // Assert
        FindToggle(sqlite, "Estimated Query Plan").ShouldNotBeNull();
        FindToggle(sqlite, "Actual Query Plan").ShouldBeNull();
        FindToggle(liteDb, "Estimated Query Plan").ShouldBeNull();
        FindToggle(liteDb, "Actual Query Plan").ShouldBeNull();
    }

    [Fact]
    public async Task ActualPlanRun_ShowsRolledBackNoticeInsteadOfError()
    {
        // Arrange
        _query.IncludeActualPlan = true;
        ((IActualQueryPlanProvider)_provider)
            .GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryPlan { PlanType = "Actual", PlanFormat = "TEXT", PlanContent = "Update on accounts" });
        var cut = RenderComponent<QueryEditor>();

        // Act
        await cut.Find(".run-query-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find(".mud-alert-text-info").TextContent.ShouldContain("rolled back");
        cut.FindAll(".mud-alert-text-error").ShouldBeEmpty();
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
