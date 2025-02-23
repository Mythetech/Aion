using Aion.Components.Querying.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Consumers;

public class SqlQueryExporter : IConsumer<ExportQueryToSql>
{
    private readonly QueryState _state;
    private readonly ILogger<SqlQueryExporter> _logger;

    public SqlQueryExporter(QueryState state, ILogger<SqlQueryExporter> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task Consume(ExportQueryToSql message)
    {
        var query = message.Query ?? _state.Active;
        
        if (query == null)
        {
            _logger.LogError("No query available to export");
            return;
        }

        try
        {
            var fileName = $"query_{DateTime.Now:yyyyMMddHHmmss}.sql";
            
            // Add metadata as comments
            var sql = $"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                     $"-- Connection: {query.ConnectionId}\n" +
                     $"-- Database: {query.DatabaseName}\n\n" +
                     query.Query;
            
            await File.WriteAllTextAsync(fileName, sql);
            
            _logger.LogInformation("Exported query to SQL: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query to SQL");
        }
    }
} 