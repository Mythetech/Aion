using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Contracts.Queries;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

/// <summary>
/// Refreshes the schema tree after a statement that creates, alters or drops something succeeds, so a table
/// made from a template or by hand shows up without a manual refresh, and counts the rows again after one that
/// changes rows. Inside a transaction the refresh waits for the commit, and a rollback leaves the tree as it was.
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
        if (message.Result is not { Success: true } result || message.ResultKind != QueryResultKind.Results
            || message.ConnectionId is not { } connectionId)
            return;

        var change = Detect(message.ExecutedSql, result);
        if (change == SchemaChange.None)
            return;

        var pending = new PendingSchemaChange(connectionId, message.DatabaseName, change);

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

    // The engine's own count catches statements the keywords miss, such as a DELETE inside a WITH.
    private static SchemaChange Detect(string sql, QueryResult result)
    {
        var change = SchemaChangeDetector.Detect(sql);
        return change == SchemaChange.None && result.RowsAffected > 0 ? SchemaChange.RowCounts : change;
    }

    private Task RefreshAsync(PendingSchemaChange change) => change.Change == SchemaChange.RowCounts
        ? _connections.RefreshRowCountsAsync(change.ConnectionId, change.DatabaseName)
        : _connections.RefreshAfterSchemaChangeAsync(change.ConnectionId, change.DatabaseName, change.Change == SchemaChange.Databases);
}
