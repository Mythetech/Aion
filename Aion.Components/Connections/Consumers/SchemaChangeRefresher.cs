using Aion.Components.Querying.Events;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

/// <summary>
/// Refreshes the schema tree after a statement that creates, alters or drops something succeeds, so a table
/// made from a template or by hand shows up without a manual refresh.
/// </summary>
public class SchemaChangeRefresher : IConsumer<QueryExecuted>
{
    private readonly ConnectionState _connections;

    public SchemaChangeRefresher(ConnectionState connections)
    {
        _connections = connections;
    }

    public async Task Consume(QueryExecuted message)
    {
        if (message.Result is not { Success: true } || message.ConnectionId is not { } connectionId)
            return;

        var change = SchemaChangeDetector.Detect(message.ExecutedSql);
        if (change == SchemaChange.None)
            return;

        await _connections.RefreshAfterSchemaChangeAsync(connectionId, message.DatabaseName, change == SchemaChange.Databases);
    }
}
