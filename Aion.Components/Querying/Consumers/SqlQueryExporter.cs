using Aion.Components.Querying.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Shared.Snackbar.Commands;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class SqlQueryExporter : IConsumer<ExportQueryToSql>
{
    private readonly QueryState _state;
    private readonly ILogger<SqlQueryExporter> _logger;
    private readonly IMessageBus _bus;

    public SqlQueryExporter(QueryState state, ILogger<SqlQueryExporter> logger, IMessageBus bus)
    {
        _state = state;
        _logger = logger;
        _bus = bus;
    }

    public async Task Consume(ExportQueryToSql message)
    {
        var query = message.Query ?? _state.Active;
        
        if (query == null)
        {
            _logger.LogError("No query available to export");
            await _bus.PublishAsync(new AddNotification("No query available to export", Severity.Warning));
            return;
        }

        try
        {
            var fileName = $"query_{DateTime.Now:yyyyMMddHHmmss}.sql";
            
            var sql = $"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                     $"-- Connection: {query.ConnectionId}\n" +
                     $"-- Database: {query.DatabaseName}\n\n" +
                     query.Query;
            
            await File.WriteAllTextAsync(fileName, sql);
            
            _logger.LogInformation("Exported query to SQL: {FileName}", fileName);
            await _bus.PublishAsync(new AddNotification($"Exported query to {fileName}", Severity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query to SQL");
            await _bus.PublishAsync(new AddNotification("Failed to export query to SQL", Severity.Error));
        }
    }
} 