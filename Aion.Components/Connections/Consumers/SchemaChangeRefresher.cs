using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

/// <summary>
/// Refreshes the schema tree after a statement that creates, alters or drops something succeeds, so a table
/// made from a template or by hand shows up without a manual refresh. Inside a transaction the refresh waits
/// for the commit, and a rollback leaves the tree as it was.
/// </summary>
public class SchemaChangeRefresher : IConsumer<QueryExecuted>, IConsumer<TransactionFinished>
{
    private readonly ConnectionState _connections;
    private readonly PendingSchemaChanges _pending;

    public SchemaChangeRefresher(ConnectionState connections, PendingSchemaChanges pending)
    {
        _connections = connections;
        _pending = pending;
    }

    public async Task Consume(QueryExecuted message)
    {
        // A plan run either never ran the statement or rolled it back.
        if (message.Result is not { Success: true } || message.ResultKind != QueryResultKind.Results
            || message.ConnectionId is not { } connectionId)
            return;

        var change = SchemaChangeDetector.Detect(message.ExecutedSql);
        if (change == SchemaChange.None)
            return;

        var pending = new PendingSchemaChange(connectionId, message.DatabaseName, change == SchemaChange.Databases);

        if (message.TransactionId is { } transactionId)
        {
            _pending.Remember(transactionId, pending);
            return;
        }

        await RefreshAsync(pending);
    }

    public async Task Consume(TransactionFinished message)
    {
        // Taken either way, so a rolled back transaction's changes are forgotten.
        if (_pending.Take(message.Transaction.Id) is { } pending && message.IsCommitted)
        {
            await RefreshAsync(pending);
        }
    }

    private Task RefreshAsync(PendingSchemaChange change) =>
        _connections.RefreshAfterSchemaChangeAsync(change.ConnectionId, change.DatabaseName, change.DatabasesChanged);
}
