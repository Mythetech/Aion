using Aion.Components.Connections.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Events;
using Aion.Core.Queries;

namespace Aion.Components.Connections.Consumers;
using Microsoft.Extensions.Logging;

public class TransactionFinalizer : 
    IConsumer<CommitTransaction>,
    IConsumer<RollbackTransaction>
{
    private readonly ConnectionState _connectionState;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<TransactionFinalizer> _logger;

    public TransactionFinalizer(ConnectionState connectionState, IMessageBus messageBus, ILogger<TransactionFinalizer> logger)
    {
        _connectionState = connectionState;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task Consume(CommitTransaction message)
    {
        if (message.Query.Transaction?.Status != TransactionStatus.Active) return;
        
        var connection = _connectionState.Connections.FirstOrDefault(x => x.Id == message.Query.ConnectionId);
        if (connection == null) return;

        try
        {
            var provider = _connectionState.GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, message.Query.DatabaseName);
            await provider.CommitTransactionAsync(connectionString, message.Query.Transaction.Value.Id);
            
            message.Query.Transaction = message.Query.Transaction.Value.WithStatus(TransactionStatus.Committed);
            await _messageBus.PublishAsync(new TransactionFinished(connection.Id, message.Query.Transaction.Value, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
        }
    }

    public async Task Consume(RollbackTransaction message)
    {
        if (message.Query.Transaction?.Status != TransactionStatus.Active) return;
        
        var connection = _connectionState.Connections.FirstOrDefault(x => x.Id == message.Query.ConnectionId);
        if (connection == null) return;

        try
        {
            var provider = _connectionState.GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, message.Query.DatabaseName);
            await provider.RollbackTransactionAsync(connectionString, message.Query.Transaction.Value.Id);
            
            message.Query.Transaction = message.Query.Transaction.Value.WithStatus(TransactionStatus.RolledBack);
            await _messageBus.PublishAsync(new TransactionFinished(connection.Id, message.Query.Transaction.Value, false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction");
        }
    }
} 