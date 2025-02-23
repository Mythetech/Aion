using ClosedXML.Excel;
using Aion.Components.Querying.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Consumers;

public class ExcelResultsExporter : IConsumer<ExportResultsToExcel>
{
    private readonly QueryState _state;
    private readonly ILogger<ExcelResultsExporter> _logger;

    public ExcelResultsExporter(QueryState state, ILogger<ExcelResultsExporter> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task Consume(ExportResultsToExcel message)
    {
        var result = message.Result ?? _state.Active?.Result;
        
        if (result == null)
        {
            _logger.LogError("No query result available to export");
            return;
        }

        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Query Results");

            // Add headers
            for (int i = 0; i < result.Columns.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = result.Columns[i];
            }

            // Add data
            for (int row = 0; row < result.Rows.Count; row++)
            {
                for (int col = 0; col < result.Columns.Count; col++)
                {
                    var value = result.Rows[row][result.Columns[col]];
                    worksheet.Cell(row + 2, col + 1).Value = value?.ToString() ?? string.Empty;
                }
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            var fileName = $"query_results_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            workbook.SaveAs(fileName);
            
            _logger.LogInformation("Exported query results to Excel: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query results to Excel");
        }
    }
} 