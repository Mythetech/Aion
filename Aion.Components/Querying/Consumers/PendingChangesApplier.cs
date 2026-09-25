using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Editing;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Queries;
using Aion.Contracts.Queries.Editing;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles ApplyPendingChanges command - generates and executes SQL for pending edits. Success is only reported
/// when every UPDATE and DELETE touched exactly the one row it targets, whenever the provider reports row counts.
/// </summary>
public class PendingChangesApplier : IConsumer<ApplyPendingChanges>
{
    private readonly PendingChangesSqlBuilder _sqlBuilder;
    private readonly IMessageBus _bus;
    private readonly ILogger<PendingChangesApplier> _logger;

    public PendingChangesApplier(
        PendingChangesSqlBuilder sqlBuilder,
        IMessageBus bus,
        ILogger<PendingChangesApplier> logger)
    {
        _sqlBuilder = sqlBuilder;
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

        PendingChangesSql plan;
        try
        {
            plan = await _sqlBuilder.BuildAsync(editableResult, editState.PendingChanges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare pending changes for {Table}", editableResult.SourceTable);
            await _bus.PublishAsync(new AddNotification($"Failed to apply changes: {ex.Message}", Severity.Error));
            return;
        }

        var generation = plan.Generation;
        if (!generation.IsValid)
        {
            await _bus.PublishAsync(new AddNotification(generation.ValidationError!, Severity.Error));
            return;
        }

        if (generation.StatementCount == 0)
        {
            await _bus.PublishAsync(new AddNotification("No SQL statements generated", Severity.Warning));
            return;
        }

        _logger.LogInformation("Applying {Count} change(s) to {Table}", generation.StatementCount, editableResult.SourceTable);

        var failure = await ExecuteAsync(plan, editableResult);
        if (failure != null)
        {
            _logger.LogWarning("Pending changes to {Table} were not applied: {Failure}", editableResult.SourceTable, failure);
            await _bus.PublishAsync(new AddNotification($"Failed to apply changes: {failure}", Severity.Error));
            return;
        }

        editState.DiscardAllChanges();

        await _bus.PublishAsync(new AddNotification(
            $"Applied {generation.StatementCount} change(s) successfully", Severity.Success));

        await _bus.PublishAsync(new RunQuery());
    }

    /// <returns>A description of what went wrong, or null when every statement succeeded.</returns>
    private async Task<string?> ExecuteAsync(PendingChangesSql plan, EditableQueryResult editableResult)
    {
        var provider = plan.Provider;
        var connectionString = plan.ConnectionString;
        string? transactionId = null;

        try
        {
            if (plan.Generation.RequiresTransaction)
            {
                var transaction = await provider.BeginTransactionAsync(connectionString);
                transactionId = transaction.Id;
                _logger.LogInformation("Started transaction {TransactionId}", transactionId);
            }

            foreach (var statement in plan.Generation.Statements)
            {
                _logger.LogDebug("Executing SQL: {Statement}", statement.Sql);

                var result = transactionId != null
                    ? await provider.ExecuteInTransactionAsync(connectionString, statement.Sql, transactionId, CancellationToken.None)
                    : await provider.ExecuteQueryAsync(connectionString, statement.Sql, CancellationToken.None);

                var problem = DescribeProblem(statement, result, editableResult);
                if (problem != null)
                {
                    return await AbandonAsync(plan, transactionId, problem, statement.ExpectsSingleRow && result.RowsAffected > 1);
                }
            }

            if (transactionId != null)
            {
                await provider.CommitTransactionAsync(connectionString, transactionId);
                _logger.LogInformation("Committed transaction {TransactionId}", transactionId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply pending changes to {Table}", editableResult.SourceTable);
            return await AbandonAsync(plan, transactionId, ex.Message, changedRows: false);
        }
    }

    private static string? DescribeProblem(GeneratedStatement statement, QueryResult result, EditableQueryResult editableResult)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return $"{DescribeChange(statement.Change, editableResult)} failed: {result.Error}";
        }

        // A null count means the provider cannot tell, which is not evidence of a problem.
        if (statement.ExpectsSingleRow && result.RowsAffected is { } affected && affected != 1)
        {
            var hint = affected == 0 ? " The row may have been changed or deleted since it was loaded." : "";
            return $"{DescribeChange(statement.Change, editableResult)} affected {affected} rows instead of 1.{hint}";
        }

        return null;
    }

    private async Task<string> AbandonAsync(PendingChangesSql plan, string? transactionId, string problem, bool changedRows)
    {
        if (transactionId == null)
        {
            return changedRows
                ? $"{problem} The statement ran outside a transaction, so those rows were changed."
                : problem;
        }

        try
        {
            await plan.Provider.RollbackTransactionAsync(plan.ConnectionString, transactionId);
            _logger.LogInformation("Rolled back transaction {TransactionId}", transactionId);
            return $"{problem} All changes were rolled back.";
        }
        catch (Exception rollbackEx)
        {
            _logger.LogError(rollbackEx, "Failed to rollback transaction {TransactionId}", transactionId);
            return $"{problem} Rolling back also failed: {rollbackEx.Message}";
        }
    }

    private static string DescribeChange(PendingChange change, EditableQueryResult editableResult)
    {
        var action = change.Type switch
        {
            ChangeType.Insert => "Insert",
            ChangeType.Update => "Update",
            ChangeType.Delete => "Delete",
            _ => change.Type.ToString()
        };

        if (change.Type == ChangeType.Insert)
        {
            return $"{action} of new row {change.RowIndex + 1}";
        }

        var key = string.Join(", ", editableResult.PrimaryKeyColumns
            .Select(pk => $"{pk} = {change.OriginalValues.GetValueOrDefault(pk)}"));
        return $"{action} of row {change.RowIndex + 1} ({key})";
    }
}
