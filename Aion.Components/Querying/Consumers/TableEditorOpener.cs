using Aion.Components.Connections;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Editing;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Core.Database;
using Aion.Core.Queries;
using Aion.Core.Queries.Editing;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles OpenTableEditor command - creates a query for the table and enters edit mode.
/// </summary>
public class TableEditorOpener : IConsumer<OpenTableEditor>
{
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IMessageBus _bus;
    private readonly ILogger<TableEditorOpener> _logger;

    public TableEditorOpener(
        ConnectionState connectionState,
        QueryState queryState,
        IMessageBus bus,
        ILogger<TableEditorOpener> logger)
    {
        _connectionState = connectionState;
        _queryState = queryState;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(OpenTableEditor message)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == message.ConnectionId);
        if (connection == null)
        {
            await _bus.PublishAsync(new AddNotification("Connection not found", Severity.Error));
            return;
        }

        var database = connection.Databases.FirstOrDefault(d => d.Name == message.DatabaseName);
        if (database == null)
        {
            await _bus.PublishAsync(new AddNotification("Database not found", Severity.Error));
            return;
        }

        try
        {
            if (!database.LoadedColumnTables.Contains(message.TableName))
            {
                await _connectionState.LoadColumnsAsync(connection, database, message.TableName);
            }

            var columns = database.TableColumns.GetValueOrDefault(message.TableName) ?? [];

            if (!columns.Any(c => c.IsPrimaryKey))
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Table '{message.TableName}' has no primary key. Edit mode requires a primary key.",
                    Severity.Warning));
            }

            var query = _queryState.AddQuery($"Edit - {message.TableName}");
            query.ConnectionId = connection.Id;
            query.DatabaseName = message.DatabaseName;
            query.Query = GenerateSelectQuery(message.TableName, connection.Type);

            query.EditMetadata = new QueryEditMetadata
            {
                SourceTable = message.TableName,
                SourceDatabase = message.DatabaseName,
                ColumnMetadata = columns.ToList(),
                IsEditMode = true
            };

            await _bus.PublishAsync(new FocusQuery(query));
            await _bus.PublishAsync(new RunQuery());

            _logger.LogInformation("Opened table editor for {Table} in {Database}", message.TableName, message.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open table editor for {Table}", message.TableName);
            await _bus.PublishAsync(new AddNotification($"Failed to open table editor: {ex.Message}", Severity.Error));
        }
    }

    private static string GenerateSelectQuery(string tableName, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.PostgreSQL => $"SELECT * FROM \"{tableName}\" LIMIT 1000",
            DatabaseType.MySQL => $"SELECT * FROM `{tableName}` LIMIT 1000",
            DatabaseType.SQLServer => $"SELECT TOP 1000 * FROM [{tableName}]",
            _ => $"SELECT * FROM \"{tableName}\" LIMIT 1000"
        };
    }
}

/// <summary>
/// Metadata stored on QueryModel for edit mode support.
/// </summary>
public class QueryEditMetadata
{
    public string? SourceTable { get; set; }
    public string? SourceDatabase { get; set; }
    public List<ColumnInfo> ColumnMetadata { get; set; } = [];
    public bool IsEditMode { get; set; }
}
