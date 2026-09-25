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
/// of the previous result.
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
        if (metadata?.IsEditMode != true)
        {
            return;
        }

        var exitReason = GetExitReason(query, metadata);
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

    private static string? GetExitReason(QueryModel query, QueryEditMetadata metadata)
    {
        if (query.ConnectionId != metadata.ConnectionId || query.DatabaseName != metadata.SourceDatabase)
        {
            return "the query now runs against a different connection or database";
        }

        var parsed = EditableQueryParser.Parse(query.Query);
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
}
