using Aion.Components.Connections;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Editing;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles EnableEditModeFromQuery command - parses the active query SQL to determine
/// the table and enables edit mode if valid.
/// </summary>
public class QueryEditModeEnabler : IConsumer<EnableEditModeFromQuery>
{
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IMessageBus _bus;
    private readonly ILogger<QueryEditModeEnabler> _logger;

    public QueryEditModeEnabler(
        ConnectionState connectionState,
        QueryState queryState,
        IMessageBus bus,
        ILogger<QueryEditModeEnabler> logger)
    {
        _connectionState = connectionState;
        _queryState = queryState;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(EnableEditModeFromQuery message)
    {
        var query = _queryState.Active;
        if (query == null)
        {
            await _bus.PublishAsync(new AddNotification("No active query", Severity.Warning));
            return;
        }

        if (query.EditMetadata?.IsEditMode == true)
        {
            await _bus.PublishAsync(new AddNotification("Already in edit mode", Severity.Info));
            return;
        }

        if (query.ConnectionId == null)
        {
            await _bus.PublishAsync(new AddNotification(
                "Query has no connection. Select a connection first.", Severity.Warning));
            return;
        }

        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == query.ConnectionId);
        if (connection == null)
        {
            await _bus.PublishAsync(new AddNotification("Connection not found", Severity.Error));
            return;
        }

        if (_connectionState.GetProvider(connection.Type) is not IDatabaseRowEditingProvider)
        {
            await _bus.PublishAsync(new AddNotification(
                $"Editing rows is not supported for {connection.Type} connections.", Severity.Warning));
            return;
        }

        var databaseName = query.DatabaseName;
        if (string.IsNullOrEmpty(databaseName))
        {
            await _bus.PublishAsync(new AddNotification(
                "Query has no database selected. Select a database first.", Severity.Warning));
            return;
        }

        var database = connection.Databases.FirstOrDefault(d => d.Name == databaseName);
        if (database == null)
        {
            await _bus.PublishAsync(new AddNotification("Database not found", Severity.Error));
            return;
        }

        var parsed = EditableQueryParser.Parse(query.Query);
        if (parsed.Target == null)
        {
            await _bus.PublishAsync(new AddNotification($"{parsed.Error}.", Severity.Warning));
            return;
        }

        var (schema, tableName, selectedColumns) = parsed.Target;

        try
        {
            if (!database.TablesLoaded)
            {
                await _connectionState.LoadTablesAsync(connection, database);
            }

            var matchedTable = database.Tables.FirstOrDefault(t =>
                t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(schema) || t.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase)));

            if (matchedTable == null)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Table '{tableName}' not found in database '{databaseName}'", Severity.Warning));
                return;
            }

            var displayName = matchedTable.DisplayName;

            if (!database.LoadedColumnTables.Contains(displayName))
            {
                await _connectionState.LoadColumnsAsync(connection, database, matchedTable.Schema, matchedTable.Name);
            }

            var columns = database.TableColumns.GetValueOrDefault(displayName) ?? [];
            var primaryKeys = columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();

            if (primaryKeys.Count == 0)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Table '{displayName}' has no primary key. Edit mode requires a primary key.",
                    Severity.Warning));
                return;
            }

            var missingKeys = selectedColumns == null
                ? []
                : primaryKeys.Where(pk => !selectedColumns.Contains(pk, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missingKeys.Count > 0)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Select the primary key column(s) {string.Join(", ", missingKeys)} to edit rows of '{displayName}'.",
                    Severity.Warning));
                return;
            }

            query.EditMetadata = new QueryEditMetadata
            {
                SourceTable = matchedTable.Name,
                SourceSchema = matchedTable.Schema,
                SourceDatabase = databaseName,
                ConnectionId = connection.Id,
                ColumnMetadata = columns.ToList(),
                IsEditMode = true
            };

            await _bus.PublishAsync(new RunQuery());

            // Re-running the query can end edit mode again, for example when the results lack the key columns.
            if (query.EditMetadata?.IsEditMode != true)
            {
                return;
            }

            _logger.LogInformation("Enabled edit mode for table {Table} from query", displayName);
            await _bus.PublishAsync(new AddNotification(
                $"Edit mode enabled for table '{displayName}'", Severity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable edit mode for table {Table}", tableName);
            await _bus.PublishAsync(new AddNotification(
                $"Failed to enable edit mode: {ex.Message}", Severity.Error));
        }
    }
}
