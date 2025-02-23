using System.Text;
using Aion.Components.Querying.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Consumers;

public class CsvResultsExporter : IConsumer<ExportResultsToCsv>
{
    private readonly QueryState _state;
    private readonly ILogger<CsvResultsExporter> _logger;

    public CsvResultsExporter(QueryState state, ILogger<CsvResultsExporter> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task Consume(ExportResultsToCsv message)
    {
        var result = message.Result ?? _state.Active?.Result;
        
        if (result == null)
        {
            _logger.LogError("No query result available to export");
            return;
        }

        try
        {
            var csv = new StringBuilder();
            
            // Add headers
            csv.AppendLine(string.Join(",", result.Columns.Select(EscapeCsvField)));

            // Add rows
            foreach (var row in result.Rows)
            {
                var fields = result.Columns.Select(col => EscapeCsvField(row[col]?.ToString() ?? string.Empty));
                csv.AppendLine(string.Join(",", fields));
            }

            var fileName = $"query_results_{DateTime.Now:yyyyMMddHHmmss}.csv";
            await File.WriteAllTextAsync(fileName, csv.ToString());
            
            _logger.LogInformation("Exported query results to CSV: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query results to CSV");
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
} 