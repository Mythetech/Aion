using System.Text.RegularExpressions;
using Aion.Components.Connections;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Core.Database;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles EnableEditModeFromQuery command - parses the active query SQL to determine
/// the table and enables edit mode if valid.
/// </summary>
public partial class QueryEditModeEnabler : IConsumer<EnableEditModeFromQuery>
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

        // Parse the SQL to extract the table name
        var tableName = ExtractTableName(query.Query, connection.Type);
        if (string.IsNullOrEmpty(tableName))
        {
            await _bus.PublishAsync(new AddNotification(
                "Could not determine table from query. Edit mode requires a simple SELECT from a single table.",
                Severity.Warning));
            return;
        }

        try
        {
            // Ensure tables are loaded for this database
            if (!database.TablesLoaded)
            {
                await _connectionState.LoadTablesAsync(connection, database);
            }

            // Verify table exists (case-insensitive match)
            var matchedTable = database.Tables.FirstOrDefault(t =>
                t.Equals(tableName, StringComparison.OrdinalIgnoreCase));

            if (matchedTable == null)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Table '{tableName}' not found in database '{databaseName}'", Severity.Warning));
                return;
            }

            tableName = matchedTable;
            // Load column metadata if needed
            if (!database.LoadedColumnTables.Contains(tableName))
            {
                await _connectionState.LoadColumnsAsync(connection, database, tableName);
            }

            var columns = database.TableColumns.GetValueOrDefault(tableName) ?? [];

            if (!columns.Any(c => c.IsPrimaryKey))
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Table '{tableName}' has no primary key. Edit mode requires a primary key.",
                    Severity.Warning));
                return;
            }

            // Enable edit mode on the current query
            query.EditMetadata = new QueryEditMetadata
            {
                SourceTable = tableName,
                SourceDatabase = databaseName,
                ColumnMetadata = columns.ToList(),
                IsEditMode = true
            };

            // Re-run the query to refresh results with edit mode enabled
            await _bus.PublishAsync(new RunQuery());

            _logger.LogInformation("Enabled edit mode for table {Table} from query", tableName);
            await _bus.PublishAsync(new AddNotification(
                $"Edit mode enabled for table '{tableName}'", Severity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable edit mode for table {Table}", tableName);
            await _bus.PublishAsync(new AddNotification(
                $"Failed to enable edit mode: {ex.Message}", Severity.Error));
        }
    }

    /// <summary>
    /// Extracts the table name from a SELECT query.
    /// Supports simple SELECT * FROM table or SELECT columns FROM table patterns.
    /// </summary>
    private static string? ExtractTableName(string sql, DatabaseType dbType)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        // Normalize whitespace
        sql = sql.Trim();

        // Check it's a SELECT statement (not INSERT, UPDATE, DELETE, etc.)
        if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return null;

        // Pattern to match: FROM "table" or FROM `table` or FROM [table] or FROM table
        // Handles quoted identifiers for different database types
        var pattern = dbType switch
        {
            DatabaseType.PostgreSQL => @"FROM\s+""?(\w+)""?",
            DatabaseType.MySQL => @"FROM\s+`?(\w+)`?",
            DatabaseType.SQLServer => @"FROM\s+\[?(\w+)\]?",
            _ => @"FROM\s+[""'`\[]?(\w+)[""'`\]]?"
        };

        var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        return null;
    }
}
