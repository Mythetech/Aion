using Aion.Components.Querying.Editing;
using Aion.Components.Querying.Events;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Keeps edit mode tied to the rows it was entered for. When a tab runs SQL that no longer reads the edit table
/// from the same connection and database, or the new results lack the primary key, edit mode ends so edits are
/// never written against the wrong table. Pending changes are dropped on every re-run because they refer to rows
/// of the previous result. Read-only rows keep their table only for foreign key links, which go quietly once the
/// SQL reads something else, since they would point from another table's columns.
/// </summary>
public class QueryEditModeGuard : IConsumer<QueryExecuted>
{
    private readonly IMessageBus _bus;

    public QueryEditModeGuard(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(QueryExecuted message)
    {
        var query = message.Query;
        var metadata = query.EditMetadata;
        if (metadata == null)
        {
            return;
        }

        if (!metadata.IsEditMode)
        {
            if (GetSourceChange(query, metadata, message.ExecutedSql) != null)
            {
                query.EditMetadata = null;
            }

            return;
        }

        var exitReason = GetExitReason(query, metadata, message.ExecutedSql);
        if (exitReason != null)
        {
            query.EditMetadata = null;
            await _bus.PublishAsync(new AddNotification($"Edit mode ended: {exitReason}.", Severity.Info));
            return;
        }

        if (metadata.EditState.HasChanges)
        {
            metadata.EditState.DiscardAllChanges();
            await _bus.PublishAsync(new AddNotification(
                "Pending edits were discarded because the query ran again.", Severity.Warning));
        }
    }

    private static string? GetExitReason(QueryModel query, QueryEditMetadata metadata, string executedSql)
    {
        if (GetSourceChange(query, metadata, executedSql) is { } sourceChange)
        {
            return sourceChange;
        }

        var result = query.Result;
        if (result is { Success: true })
        {
            var missingKeys = metadata.ColumnMetadata
                .Where(c => c.IsPrimaryKey && !result.Columns.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                .Select(c => c.Name)
                .ToList();

            if (missingKeys.Count > 0)
            {
                return $"the results do not include the primary key column(s) {string.Join(", ", missingKeys)}";
            }
        }

        return null;
    }

    // Why the rows no longer come from the metadata's table, or null while they still do.
    private static string? GetSourceChange(QueryModel query, QueryEditMetadata metadata, string executedSql)
    {
        if (query.ConnectionId != metadata.ConnectionId || query.DatabaseName != metadata.SourceDatabase)
        {
            return "the query now runs against a different connection or database";
        }

        var parsed = EditableQueryParser.Parse(executedSql);
        if (parsed.Target == null)
        {
            return $"the query is no longer a simple SELECT from '{metadata.SourceTable}'";
        }

        var target = parsed.Target;
        var sameTable = target.Table.Equals(metadata.SourceTable, StringComparison.OrdinalIgnoreCase)
            && (target.Schema == null || target.Schema.Equals(metadata.SourceSchema ?? "", StringComparison.OrdinalIgnoreCase));
        if (!sameTable)
        {
            return $"the query no longer reads from '{metadata.SourceTable}'";
        }

        return null;
    }
}
