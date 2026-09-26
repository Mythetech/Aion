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
/// Handles OpenTableEditor command - creates a query for the table and enters edit mode when rows can be
/// targeted safely, otherwise opens the table read-only and says why.
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
            var displayName = string.IsNullOrEmpty(message.Schema) ? message.TableName : $"{message.Schema}.{message.TableName}";
            var provider = _connectionState.GetProvider(connection.Type);

            var columnsState = await _connectionState.LoadColumnsAsync(connection, database, message.Schema, message.TableName);
            if (columnsState.IsFailed)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Failed to open table editor: could not read the columns of '{displayName}'. {columnsState.Error}", Severity.Error));
                return;
            }

            var columns = database.TableColumns.GetValueOrDefault(displayName) ?? [];

            string? readOnlyReason = null;
            if (provider is not IDatabaseRowEditingProvider)
            {
                readOnlyReason = $"editing rows is not supported for {connection.Type} connections";
            }
            else if (!columns.Any(c => c.IsPrimaryKey))
            {
                readOnlyReason = "it has no primary key, so edited rows could not be matched safely";
            }

            var selectSql = await provider.Commands.GenerateSelectTopScript(message.DatabaseName, message.Schema, message.TableName, 1000);

            var query = _queryState.AddQuery(readOnlyReason == null ? $"Edit - {displayName}" : TableRowsOpener.TabName(displayName, 1000));
            query.ConnectionId = connection.Id;
            query.DatabaseName = message.DatabaseName;
            query.Query = selectSql.Trim();

            if (readOnlyReason == null)
            {
                query.EditMetadata = new QueryEditMetadata
                {
                    SourceTable = message.TableName,
                    SourceSchema = message.Schema,
                    SourceDatabase = message.DatabaseName,
                    ConnectionId = connection.Id,
                    ColumnMetadata = columns.ToList(),
                    IsEditMode = true
                };
            }

            await _bus.PublishAsync(new FocusQuery(query));

            if (readOnlyReason != null)
            {
                await _bus.PublishAsync(new AddNotification(
                    $"Opened '{displayName}' read-only: {readOnlyReason}.", Severity.Warning));
            }

            await _bus.PublishAsync(new RunQuery());

            _logger.LogInformation("Opened table editor for {Table} in {Database}", displayName, message.DatabaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open table editor for {Table}", message.TableName);
            await _bus.PublishAsync(new AddNotification($"Failed to open table editor: {ex.Message}", Severity.Error));
        }
    }
}

/// <summary>
/// Metadata stored on QueryModel for edit mode support. Pending changes live here too, so leaving edit mode
/// or pointing it at another table can never carry edits over to rows they were not made against.
/// </summary>
public class QueryEditMetadata
{
    public string? SourceTable { get; set; }
    public string? SourceSchema { get; set; }
    public string? SourceDatabase { get; set; }

    /// <summary>
    /// The connection the edited rows were read from, which may differ from the tab's current connection.
    /// </summary>
    public Guid? ConnectionId { get; set; }

    public List<ColumnInfo> ColumnMetadata { get; set; } = [];
    public bool IsEditMode { get; set; }
    public EditState EditState { get; } = new() { IsEditMode = true };
}
