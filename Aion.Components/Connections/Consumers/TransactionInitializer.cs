using Aion.Components.Connections.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Events;

namespace Aion.Components.Connections.Consumers;

using Microsoft.Extensions.Logging;

public class TransactionInitializer : IConsumer<StartTransaction>
{
    private readonly ConnectionState _connectionState;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<TransactionInitializer> _logger;

    public TransactionInitializer(
        ConnectionState connectionState, 
        IMessageBus messageBus,
        ILogger<TransactionInitializer> logger)
    {
        _connectionState = connectionState;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task Consume(StartTransaction message)
    {
        if (message.Query.Transaction != null)
            return;
        
        message.Query.UseTransaction = true;

        var connection = _connectionState.Connections.FirstOrDefault(x => x.Id == message.Query.ConnectionId);
        if (connection == null) return;

        try
        {
            var provider = _connectionState.GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, message.Query.DatabaseName);
            message.Query.Transaction = await provider.BeginTransactionAsync(connectionString);
            _logger.LogInformation("Started transaction {Id} for query {QueryId}", 
                message.Query.Transaction.Value.Id, message.Query.Id);
            await _messageBus.PublishAsync(new TransactionStarted(connection.Id, message.Query.Transaction.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize transaction for query {QueryId}", message.Query.Id);
            throw;
        }
    }
} 