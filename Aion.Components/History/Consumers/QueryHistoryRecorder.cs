using Aion.Components.Connections;
using Aion.Components.Querying.Events;
using Aion.Contracts.Queries;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.History.Consumers;

public class QueryHistoryRecorder : IConsumer<QueryExecuted>
{
    private readonly HistoryState _state;
    private readonly ConnectionState _connections;

    public QueryHistoryRecorder(HistoryState state, ConnectionState connections)
    {
        _state = state;
        _connections = connections;
    }

    public async Task Consume(QueryExecuted message)
    {
        await _state.AddAsync(CreateEntry(message));
    }

    private QueryHistoryEntry CreateEntry(QueryExecuted message)
    {
        var result = message.Result;
        var status = GetStatus(result);

        return new QueryHistoryEntry
        {
            Sql = message.ExecutedSql,
            ConnectionId = message.ConnectionId,
            ConnectionName = _connections.Connections.FirstOrDefault(c => c.Id == message.ConnectionId)?.Name,
            DatabaseName = message.DatabaseName,
            Status = status,
            ErrorMessage = status == QueryHistoryStatus.Failed ? result?.Error : null,
            RowCount = status == QueryHistoryStatus.Success ? result?.RowCount : null,
            RowsAffected = status == QueryHistoryStatus.Success ? result?.RowsAffected : null,
            Duration = message.Duration,
            ExecutedAt = message.ExecutedAt
        };
    }

    private static QueryHistoryStatus GetStatus(QueryResult? result) => result switch
    {
        { Cancelled: true } => QueryHistoryStatus.Cancelled,
        { Success: false } => QueryHistoryStatus.Failed,
        _ => QueryHistoryStatus.Success
    };
}
