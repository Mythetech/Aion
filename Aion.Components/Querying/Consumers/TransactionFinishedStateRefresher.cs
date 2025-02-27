using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying.Events;

namespace Aion.Components.Querying.Consumers;

public class TransactionFinishedStateRefresher : IConsumer<TransactionFinished>
{
    private readonly QueryState _state;

    public TransactionFinishedStateRefresher(QueryState state)
    {
        _state = state;
    }

    public Task Consume(TransactionFinished message)
    {
        _state.SetTransactionInfo(message.Transaction);
        return Task.CompletedTask;
    }
}