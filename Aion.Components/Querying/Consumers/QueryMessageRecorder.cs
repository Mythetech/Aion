using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Events;
using Aion.Components.Querying.Messages;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Writes each tab's runs and transactions to its Messages log.
/// </summary>
public class QueryMessageRecorder :
    IConsumer<TransactionStarted>,
    IConsumer<QueryExecuted>,
    IConsumer<TransactionFinished>,
    IConsumer<DeleteQuery>
{
    private readonly QueryMessageLog _log;

    public QueryMessageRecorder(QueryMessageLog log)
    {
        _log = log;
    }

    public Task Consume(TransactionStarted message)
    {
        var startedAt = new DateTimeOffset(DateTime.SpecifyKind(message.Transaction.StartTime, DateTimeKind.Utc));
        _log.RecordBegin(message.QueryId, startedAt);
        return Task.CompletedTask;
    }

    public Task Consume(QueryExecuted message)
    {
        _log.RecordRun(message.Query.Id, message.ExecutedSql, message.Result, message.ExecutedAt, message.Duration, message.ResultKind);
        return Task.CompletedTask;
    }

    public Task Consume(TransactionFinished message)
    {
        _log.RecordEnd(message.QueryId, message.IsCommitted, DateTimeOffset.Now);
        return Task.CompletedTask;
    }

    public Task Consume(DeleteQuery message)
    {
        _log.Forget(message.Query.Id);
        return Task.CompletedTask;
    }
}
