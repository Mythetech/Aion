using Aion.Components.Metrics.Consumers;
using Aion.Components.Metrics.Events;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Metrics;

public class MetricsQueryConsumerTests
{
    private readonly IMessageBus _bus;
    private readonly MetricsQueryConsumer _consumer;

    public MetricsQueryConsumerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _consumer = new MetricsQueryConsumer(_bus);
    }

    [Fact]
    public async Task Consume_SuccessfulQuery_PublishesMetricWithCorrectFields()
    {
        var query = new QueryModel
        {
            ConnectionId = Guid.NewGuid(),
            DatabaseName = "testdb"
        };
        query.StartExecution();
        await Task.Delay(10);
        query.SetResult(new Aion.Contracts.Queries.QueryResult
        {
            Rows = [new Dictionary<string, object> { ["col"] = 1 }]
        });

        await _consumer.Consume(new QueryExecuted(query));

        await _bus.Received(1).PublishAsync(
            Arg.Is<ClientQueryMetricRecorded>(m =>
                m.ConnectionId == query.ConnectionId!.Value &&
                m.RowCount == 1 &&
                m.Success &&
                m.Duration > TimeSpan.Zero));
    }

    [Fact]
    public async Task Consume_NoConnectionId_DoesNotPublish()
    {
        var query = new QueryModel { ConnectionId = null };

        await _consumer.Consume(new QueryExecuted(query));

        await _bus.DidNotReceive().PublishAsync(Arg.Any<ClientQueryMetricRecorded>());
    }

    [Fact]
    public async Task Consume_NoExecutionTiming_DoesNotPublish()
    {
        var query = new QueryModel { ConnectionId = Guid.NewGuid() };

        await _consumer.Consume(new QueryExecuted(query));

        await _bus.DidNotReceive().PublishAsync(Arg.Any<ClientQueryMetricRecorded>());
    }
}
