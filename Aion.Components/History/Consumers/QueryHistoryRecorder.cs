using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;

namespace Aion.Components.History.Consumers;

public class QueryHistoryRecorder : IConsumer<QueryExecuted>
{
    private readonly HistoryState _state;

    public QueryHistoryRecorder(HistoryState state)
    {
        _state = state;
    }

    public Task Consume(QueryExecuted message)
    {
        _state.AddQuery(message.Query);
        return Task.CompletedTask;
    }
}