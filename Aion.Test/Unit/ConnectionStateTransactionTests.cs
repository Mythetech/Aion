using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStateTransactionTests
{
    private const string DbConnectionString = "db-connection";

    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, IActualQueryPlanProvider, IEstimatedQueryPlanProvider>();
    private readonly ConnectionModel _connection = new()
    {
        Name = "Test",
        ConnectionString = "Host=localhost",
        Type = DatabaseType.PostgreSQL,
        Active = true
    };
    private readonly ConnectionState _sut;
    private readonly QueryModel _query;

    public ConnectionStateTransactionTests()
    {
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(DbConnectionString);
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new QueryResult());
        _provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());
        _provider.BeginTransactionAsync(Arg.Any<string>()).Returns(_ => new TransactionInfo());
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);

        _sut = new ConnectionState(Substitute.For<IConnectionService>(), _factory, _bus, NullLogger<ConnectionState>.Instance)
        {
            Connections = [_connection]
        };

        _query = new QueryModel
        {
            Name = "Query1",
            Query = "UPDATE accounts SET balance = 0",
            ConnectionId = _connection.Id,
            DatabaseName = "db"
        };
    }

    private IActualQueryPlanProvider ActualPlans => (IActualQueryPlanProvider)_provider;

    [Fact]
    public async Task Run_WithTransactionsOn_BeginsTransactionAndRunsStatementInsideIt()
    {
        // Arrange
        _query.UseTransaction = true;

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _query.HasOpenTransaction.ShouldBeTrue();
        _query.Transaction!.Value.StatementCount.ShouldBe(1);
        await _provider.Received(1).ExecuteInTransactionAsync(
            DbConnectionString, _query.Query, _query.Transaction.Value.Id, Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<TransactionStarted>());
    }

    [Fact]
    public async Task Run_WithOpenTransaction_ReusesItForEveryStatement()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        var transactionId = _query.Transaction!.Value.Id;

        // Act
        _query.Query = "DELETE FROM accounts";
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        await _provider.Received(1).BeginTransactionAsync(Arg.Any<string>());
        await _provider.Received(1).ExecuteInTransactionAsync(DbConnectionString, "DELETE FROM accounts", transactionId, Arg.Any<CancellationToken>());
        _query.Transaction!.Value.Id.ShouldBe(transactionId);
        _query.Transaction.Value.StatementCount.ShouldBe(2);
    }

    [Fact]
    public async Task Run_WithOpenTransaction_CountsOnlyStatementsThatSucceeded()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        _provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult { Error = "syntax error" });

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Error.ShouldBe("syntax error");
        _query.HasOpenTransaction.ShouldBeTrue();
        _query.Transaction!.Value.StatementCount.ShouldBe(1);
    }

    private void NextStatementReturns(QueryResult result) =>
        _provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task Run_WithOpenTransaction_AddsUpTheRowsEachStatementChanged()
    {
        // Arrange
        _query.UseTransaction = true;
        NextStatementReturns(new QueryResult { RowsAffected = 2 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        NextStatementReturns(new QueryResult { Columns = ["id"], Rows = [new() { ["id"] = 1 }] });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Act
        NextStatementReturns(new QueryResult { RowsAffected = 3 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        _query.Transaction!.Value.StatementCount.ShouldBe(3);
        _query.Transaction.Value.RowsChanged.ShouldBe(5);
    }

    [Fact]
    public async Task Run_WithOpenTransaction_DoesNotCountRowsOfAFailedStatement()
    {
        // Arrange
        _query.UseTransaction = true;
        NextStatementReturns(new QueryResult { RowsAffected = 2 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Act
        NextStatementReturns(new QueryResult { Error = "duplicate key", RowsAffected = 4 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        _query.Transaction!.Value.RowsChanged.ShouldBe(2);
    }

    [Fact]
    public async Task Run_AfterCommit_StartsCountingRowsFromZero()
    {
        // Arrange
        _query.UseTransaction = true;
        NextStatementReturns(new QueryResult { RowsAffected = 7 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        await _sut.CommitTransactionAsync(_query);

        // Act
        NextStatementReturns(new QueryResult { RowsAffected = 1 });
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        _query.Transaction!.Value.StatementCount.ShouldBe(1);
        _query.Transaction.Value.RowsChanged.ShouldBe(1);
    }

    [Fact]
    public async Task Run_WithOpenTransactionAndToggleOff_StillRunsInsideTransaction()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        _query.UseTransaction = false;

        // Act
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        await _provider.Received(2).ExecuteInTransactionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WhenBeginFails_DoesNotRunStatementAndReportsError()
    {
        // Arrange
        _query.UseTransaction = true;
        _provider.BeginTransactionAsync(Arg.Any<string>()).ThrowsAsync(new InvalidOperationException("connection refused"));

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("connection refused");
        _query.Transaction.ShouldBeNull();
        _query.IsExecuting.ShouldBeFalse();
        _query.ExecutionEndTime.ShouldNotBeNull();
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_ClearsTransactionOnOwningQueryAndPublishesTransactionFinished()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        var transactionId = _query.Transaction!.Value.Id;

        // Act
        var committed = await _sut.CommitTransactionAsync(_query);

        // Assert
        committed.ShouldBeTrue();
        _query.Transaction.ShouldBeNull();
        await _provider.Received(1).CommitTransactionAsync(DbConnectionString, transactionId);
        await _bus.Received(1).PublishAsync(Arg.Is<TransactionFinished>(e =>
            e.QueryId == _query.Id && e.IsCommitted && e.Transaction.Status == TransactionStatus.Committed));
    }

    [Fact]
    public async Task Run_AfterCommit_StartsANewTransaction()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        var firstId = _query.Transaction!.Value.Id;
        await _sut.CommitTransactionAsync(_query);

        // Act
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        await _provider.Received(2).BeginTransactionAsync(Arg.Any<string>());
        _query.HasOpenTransaction.ShouldBeTrue();
        _query.Transaction!.Value.Id.ShouldNotBe(firstId);
    }

    [Fact]
    public async Task Commit_WhenProviderFails_KeepsTransactionOpenAndNotifies()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        _provider.CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("transaction aborted"));

        // Act
        var committed = await _sut.CommitTransactionAsync(_query);

        // Assert
        committed.ShouldBeFalse();
        _query.HasOpenTransaction.ShouldBeTrue();
        await _bus.Received(1).PublishAsync(Arg.Is<AddNotification>(n =>
            n.Severity == Severity.Error && n.Message.Contains("transaction aborted")));
        await _bus.DidNotReceive().PublishAsync(Arg.Any<TransactionFinished>());
    }

    [Fact]
    public async Task Rollback_ClearsTransactionAndReleasesItInProvider()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        var transactionId = _query.Transaction!.Value.Id;

        // Act
        await _sut.RollbackTransactionAsync(_query);

        // Assert
        _query.Transaction.ShouldBeNull();
        await _provider.Received(1).RollbackTransactionAsync(DbConnectionString, transactionId);
        await _bus.Received(1).PublishAsync(Arg.Is<TransactionFinished>(e => e.QueryId == _query.Id && !e.IsCommitted));
    }

    [Fact]
    public async Task Rollback_WhenProviderFails_StillClearsTransactionAndWarns()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        _provider.RollbackTransactionAsync(Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        // Act
        await _sut.RollbackTransactionAsync(_query);

        // Assert
        _query.Transaction.ShouldBeNull();
        await _bus.Received(1).PublishAsync(Arg.Is<AddNotification>(n =>
            n.Severity == Severity.Warning && n.Message.Contains("connection lost")));
    }

    [Fact]
    public async Task ActualPlan_ReturnsNormalResultWithRolledBackNoticeAndStopsTimer()
    {
        // Arrange
        _query.IncludeActualPlan = true;
        var plan = new QueryPlan { PlanType = "Actual", PlanContent = "Update on accounts" };
        ActualPlans.GetActualPlanAsync(DbConnectionString, _query.Query, Arg.Any<CancellationToken>()).Returns(plan);

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _query.ActualPlan.ShouldBe(plan);
        _query.ResultNotice.ShouldBe(ConnectionState.ActualPlanNotice);
        _query.IsExecuting.ShouldBeFalse();
        _query.ExecutionEndTime.ShouldNotBeNull();
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualPlan_PassesCancellationTokenToProvider()
    {
        // Arrange
        _query.IncludeActualPlan = true;
        using var cancellation = new CancellationTokenSource();
        ActualPlans.GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryPlan());

        // Act
        await _sut.ExecuteQueryAsync(_query, cancellation.Token);

        // Assert
        await ActualPlans.Received(1).GetActualPlanAsync(DbConnectionString, _query.Query, cancellation.Token);
    }

    [Fact]
    public async Task ActualPlan_WhenCaptureFails_ReturnsErrorInsteadOfPlan()
    {
        // Arrange
        _query.IncludeActualPlan = true;
        ActualPlans.GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("statement refused"));

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Error.ShouldBe("statement refused");
        _query.ActualPlan.ShouldBeNull();
        _query.ResultNotice.ShouldBeNull();
        _query.ExecutionEndTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task ActualPlan_WithOpenTransaction_IsRefusedWithoutRunningAnything()
    {
        // Arrange
        _query.UseTransaction = true;
        await _sut.ExecuteQueryAsync(_query, CancellationToken.None);
        _query.IncludeActualPlan = true;

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Error.ShouldBe(ConnectionState.ActualPlanInTransactionMessage);
        _query.HasOpenTransaction.ShouldBeTrue();
        await ActualPlans.DidNotReceive().GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Plans_OnProviderWithoutCapabilities_AreSkippedAndStatementRunsNormally()
    {
        // Arrange
        var basicProvider = Substitute.For<IDatabaseProvider>();
        basicProvider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(DbConnectionString);
        basicProvider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new QueryResult());
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(basicProvider);
        _query.IncludeActualPlan = true;
        _query.IncludeEstimatedPlan = true;

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _query.ActualPlan.ShouldBeNull();
        _query.EstimatedPlan.ShouldBeNull();
        _query.ResultNotice.ShouldBeNull();
        await basicProvider.Received(1).ExecuteQueryAsync(DbConnectionString, _query.Query, Arg.Any<CancellationToken>());
        await basicProvider.DidNotReceive().GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>());
        await basicProvider.DidNotReceive().GetEstimatedPlanAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void SupportsPlans_ReflectsProviderCapabilities()
    {
        // Arrange
        var estimatedOnly = Substitute.For<IDatabaseProvider, IEstimatedQueryPlanProvider>();
        var none = Substitute.For<IDatabaseProvider>();
        _factory.GetProvider(DatabaseType.WasmSQLite).Returns(estimatedOnly);
        _factory.GetProvider(DatabaseType.LiteDB).Returns(none);

        // Act & Assert
        _sut.SupportsEstimatedPlan(DatabaseType.PostgreSQL).ShouldBeTrue();
        _sut.SupportsActualPlan(DatabaseType.PostgreSQL).ShouldBeTrue();
        _sut.SupportsEstimatedPlan(DatabaseType.WasmSQLite).ShouldBeTrue();
        _sut.SupportsActualPlan(DatabaseType.WasmSQLite).ShouldBeFalse();
        _sut.SupportsEstimatedPlan(DatabaseType.LiteDB).ShouldBeFalse();
        _sut.SupportsActualPlan(DatabaseType.LiteDB).ShouldBeFalse();
    }
}
