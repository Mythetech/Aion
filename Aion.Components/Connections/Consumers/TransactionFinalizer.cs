using Aion.Components.Connections.Commands;
using Aion.Components.Querying.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

public class TransactionFinalizer :
    IConsumer<CommitTransaction>,
    IConsumer<RollbackTransaction>,
    IConsumer<DeleteQuery>
{
    private readonly ConnectionState _connectionState;

    public TransactionFinalizer(ConnectionState connectionState)
    {
        _connectionState = connectionState;
    }

    public async Task Consume(CommitTransaction message)
    {
        await _connectionState.CommitTransactionAsync(message.Query);
    }

    public async Task Consume(RollbackTransaction message)
    {
        await _connectionState.RollbackTransactionAsync(message.Query);
    }

    /// <summary>
    /// A closed tab can no longer commit, so its open transaction is rolled back to release the
    /// connection and any locks it holds.
    /// </summary>
    public async Task Consume(DeleteQuery message)
    {
        if (!message.Query.HasOpenTransaction) return;

        await _connectionState.RollbackTransactionAsync(message.Query,
            $"Rolled back the open transaction in \"{message.Query.Name}\" because its tab was closed.");
    }
}
