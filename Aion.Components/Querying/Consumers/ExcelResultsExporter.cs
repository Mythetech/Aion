using ClosedXML.Excel;
using Aion.Components.Querying.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Shared.Snackbar.Commands;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class ExcelResultsExporter : IConsumer<ExportResultsToExcel>
{
    private readonly QueryState _state;
    private readonly ILogger<ExcelResultsExporter> _logger;
    private readonly IMessageBus _bus;

    public ExcelResultsExporter(QueryState state, ILogger<ExcelResultsExporter> logger, IMessageBus bus)
    {
        _state = state;
        _logger = logger;
        _bus = bus;
    }

    public async Task Consume(ExportResultsToExcel message)
    {
        var result = message.Result ?? _state.Active?.Result;
        
        if (result == null)
        {
            _logger.LogWarning("No query result available to export");
            await _bus.PublishAsync(new AddNotification("No results available to export", Severity.Warning));
            return;
        }

        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Query Results");

            for (int i = 0; i < result.Columns.Count; i++)
            {
                worksheet.Cell(1, i + 1).Value = result.Columns[i];
            }

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
            await _bus.PublishAsync(new AddNotification($"Exported results to {fileName}", Severity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query results to Excel");
            await _bus.PublishAsync(new AddNotification("Failed to export results to Excel", Severity.Error));
        }
    }
} 