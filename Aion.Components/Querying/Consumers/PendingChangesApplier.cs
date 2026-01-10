using Aion.Components.Connections;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Editing;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Core.Database;
using Aion.Core.Queries.Editing;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles ApplyPendingChanges command - generates and executes SQL for pending edits.
/// </summary>
public class PendingChangesApplier : IConsumer<ApplyPendingChanges>
{
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IMessageBus _bus;
    private readonly ILogger<PendingChangesApplier> _logger;

    public PendingChangesApplier(
        ConnectionState connectionState,
        QueryState queryState,
        IMessageBus bus,
        ILogger<PendingChangesApplier> logger)
    {
        _connectionState = connectionState;
        _queryState = queryState;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(ApplyPendingChanges message)
    {
        var editState = message.EditState;
        var editableResult = message.EditableResult;

        if (!editState.HasChanges)
        {
            await _bus.PublishAsync(new AddNotification("No changes to apply", Severity.Info));
            return;
        }

        try
        {
            var connection = _connectionState.Connections
                .FirstOrDefault(c => c.Id == editableResult.ConnectionId);
            if (connection == null)
            {
                await _bus.PublishAsync(new AddNotification("Connection not found", Severity.Error));
                return;
            }

            var provider = _connectionState.GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(
                connection.ConnectionString,
                editableResult.SourceDatabase ?? "");
            var commands = provider.Commands;

            var generator = new SqlChangeGenerator();
            var result = await generator.GenerateSqlAsync(editableResult, editState.PendingChanges, commands);

            if (!result.IsValid)
            {
                await _bus.PublishAsync(new AddNotification(result.ValidationError!, Severity.Error));
                return;
            }

            if (result.StatementCount == 0)
            {
                await _bus.PublishAsync(new AddNotification("No SQL statements generated", Severity.Warning));
                return;
            }

            _logger.LogInformation("Applying {Count} change(s) to {Table}",
                result.StatementCount, editableResult.SourceTable);

            string? transactionId = null;
            try
            {
                if (result.RequiresTransaction)
                {
                    var transaction = await provider.BeginTransactionAsync(connectionString);
                    transactionId = transaction.Id;
                    _logger.LogInformation("Started transaction {TransactionId}", transactionId);
                }

                foreach (var statement in result.Statements)
                {
                    _logger.LogInformation("Executing SQL: {Statement}", statement);

                    Core.Queries.QueryResult queryResult;
                    if (transactionId != null)
                    {
                        queryResult = await provider.ExecuteInTransactionAsync(
                            connectionString, statement, transactionId, CancellationToken.None);
                    }
                    else
                    {
                        queryResult = await provider.ExecuteQueryAsync(connectionString, statement, CancellationToken.None);
                    }

                    if (!string.IsNullOrEmpty(queryResult.Error))
                    {
                        throw new Exception($"SQL execution failed: {queryResult.Error}");
                    }
                }

                if (transactionId != null)
                {
                    await provider.CommitTransactionAsync(connectionString, transactionId);
                    _logger.LogInformation("Committed transaction {TransactionId}", transactionId);
                }

                editState.DiscardAllChanges();

                await _bus.PublishAsync(new AddNotification(
                    $"Applied {result.StatementCount} change(s) successfully", Severity.Success));

                await _bus.PublishAsync(new RunQuery());
            }
            catch (Exception ex)
            {
                if (transactionId != null)
                {
                    try
                    {
                        await provider.RollbackTransactionAsync(connectionString, transactionId);
                        _logger.LogInformation("Rolled back transaction {TransactionId}", transactionId);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError(rollbackEx, "Failed to rollback transaction {TransactionId}", transactionId);
                    }
                }

                throw; 
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply pending changes to {Table}", editableResult.SourceTable);
            await _bus.PublishAsync(new AddNotification(
                $"Failed to apply changes: {ex.Message}", Severity.Error));
        }
    }
}
