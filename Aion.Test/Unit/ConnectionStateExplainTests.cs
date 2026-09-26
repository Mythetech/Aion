using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStateExplainTests
{
    private const string DbConnectionString = "db-connection";
    private const string Sql = "SELECT name FROM products";

    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, IActualQueryPlanProvider, IEstimatedQueryPlanProvider>();
    private readonly QueryPlan _estimated = new() { PlanType = "Estimated", PlanFormat = "TEXT", PlanContent = "Seq Scan on products" };
    private readonly QueryPlan _actual = new() { PlanType = "Actual", PlanFormat = "TEXT", PlanContent = "Seq Scan on products (actual rows=3)" };
    private readonly ConnectionState _sut;
    private readonly QueryModel _query;

    public ConnectionStateExplainTests()
    {
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(DbConnectionString);
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new QueryResult());
        EstimatedPlans.GetEstimatedPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_estimated);
        ActualPlans.GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_actual);
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(_provider);

        var connection = new ConnectionModel { Name = "Test", ConnectionString = "Host=localhost", Type = DatabaseType.PostgreSQL };
        _sut = new ConnectionState(Substitute.For<IConnectionService>(), _factory, _bus, NullLogger<ConnectionState>.Instance)
        {
            Connections = [connection]
        };
        _query = new QueryModel { Query = Sql, ConnectionId = connection.Id, DatabaseName = "db" };
    }

    private IEstimatedQueryPlanProvider EstimatedPlans => (IEstimatedQueryPlanProvider)_provider;

    private IActualQueryPlanProvider ActualPlans => (IActualQueryPlanProvider)_provider;

    [Fact]
    public async Task Explain_GetsTheEstimatedPlanWithoutRunningTheStatement()
    {
        // Act
        var result = await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.Explain, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _query.EstimatedPlan.ShouldBe(_estimated);
        _query.ResultKind.ShouldBe(QueryResultKind.EstimatedPlan);
        _query.IsExecuting.ShouldBeFalse();
        await EstimatedPlans.Received(1).GetEstimatedPlanAsync(DbConnectionString, Sql, Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(Arg.Any<QueryExecuted>());
    }

    [Fact]
    public async Task Explain_WithTheActualPlanToggleOn_StillOnlyEstimates()
    {
        // Arrange
        _query.IncludeActualPlan = true;

        // Act
        await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.Explain, CancellationToken.None);

        // Assert
        await ActualPlans.DidNotReceive().GetActualPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explain_WhenThePlannerRefuses_ReportsAnErrorAndDropsTheOldPlan()
    {
        // Arrange
        _query.EstimatedPlan = _estimated;
        EstimatedPlans.GetEstimatedPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("syntax error at or near \"FORM\""));

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.Explain, CancellationToken.None);

        // Assert
        result.Error.ShouldBe("syntax error at or near \"FORM\"");
        _query.EstimatedPlan.ShouldBeNull();
        _query.ResultKind.ShouldBe(QueryResultKind.Results);
    }

    [Fact]
    public async Task ExplainAnalyze_CapturesTheActualPlanWhileTheToggleIsOff()
    {
        // Arrange
        _query.IncludeEstimatedPlan = true;

        // Act
        var result = await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.ExplainAnalyze, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        _query.ActualPlan.ShouldBe(_actual);
        _query.IncludeActualPlan.ShouldBeFalse();
        _query.ResultKind.ShouldBe(QueryResultKind.ActualPlan);
        await EstimatedPlans.DidNotReceive().GetEstimatedPlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explain_OnAnEngineWithoutPlans_SaysSoWithoutRunningTheStatement()
    {
        // Arrange
        var basic = Substitute.For<IDatabaseProvider>();
        basic.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(DbConnectionString);
        _factory.GetProvider(DatabaseType.PostgreSQL).Returns(basic);

        // Act
        var explain = await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.Explain, CancellationToken.None);
        var analyze = await _sut.ExecuteQueryAsync(_query, Sql, QueryRunKind.ExplainAnalyze, CancellationToken.None);

        // Assert
        explain.Error.ShouldBe(ConnectionState.NoEstimatedPlansMessage);
        analyze.Error.ShouldBe(ConnectionState.NoActualPlansMessage);
        await basic.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
