using System.Text;
using System.Text.Json;
using Aion.Components.Infrastructure;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Exports selected rows to CSV, JSON, or Excel files.
/// </summary>
public class SelectedRowsExporter : IConsumer<ExportSelectedRows>
{
    private readonly ILogger<SelectedRowsExporter> _logger;
    private readonly IMessageBus _bus;
    private readonly IFileSaveService _saveService;

    public SelectedRowsExporter(ILogger<SelectedRowsExporter> logger, IMessageBus bus, IFileSaveService saveService)
    {
        _logger = logger;
        _bus = bus;
        _saveService = saveService;
    }

    public async Task Consume(ExportSelectedRows message)
    {
        if (message.Rows.Count == 0)
        {
            await _bus.PublishAsync(new AddNotification("No rows selected to export", Severity.Warning));
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            switch (message.Format.ToLowerInvariant())
            {
                case "csv":
                    await ExportToCsvAsync(message, timestamp);
                    break;
                case "json":
                    await ExportToJsonAsync(message, timestamp);
                    break;
                case "excel":
                    await ExportToExcelAsync(message, timestamp);
                    break;
                default:
                    await _bus.PublishAsync(new AddNotification($"Unknown export format: {message.Format}", Severity.Error));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export selected rows to {Format}", message.Format);
            await _bus.PublishAsync(new AddNotification($"Failed to export rows to {message.Format}", Severity.Error));
        }
    }

    private async Task ExportToCsvAsync(ExportSelectedRows message, string timestamp)
    {
        var csv = new StringBuilder();

        // Header
        csv.AppendLine(string.Join(",", message.Columns.Select(EscapeCsvField)));

        // Data rows
        foreach (var row in message.Rows)
        {
            var fields = message.Columns.Select(col =>
                EscapeCsvField(row.TryGetValue(col, out var val) ? val?.ToString() ?? "" : ""));
            csv.AppendLine(string.Join(",", fields));
        }

        var fileName = $"selected_rows_{timestamp}.csv";
        var success = await _saveService.SaveFileAsync(fileName, csv.ToString());

        if (success)
        {
            _logger.LogInformation("Exported {Count} rows to CSV: {FileName}", message.Rows.Count, fileName);
            await _bus.PublishAsync(new AddNotification($"Exported {message.Rows.Count} rows to {fileName}", Severity.Success));
        }
        else
        {
            await _bus.PublishAsync(new AddNotification("CSV export cancelled", Severity.Info));
        }
    }

    private async Task ExportToJsonAsync(ExportSelectedRows message, string timestamp)
    {
        var json = JsonSerializer.Serialize(message.Rows, new JsonSerializerOptions { WriteIndented = true });

        var fileName = $"selected_rows_{timestamp}.json";
        var success = await _saveService.SaveFileAsync(fileName, json);

        if (success)
        {
            _logger.LogInformation("Exported {Count} rows to JSON: {FileName}", message.Rows.Count, fileName);
            await _bus.PublishAsync(new AddNotification($"Exported {message.Rows.Count} rows to {fileName}", Severity.Success));
        }
        else
        {
            await _bus.PublishAsync(new AddNotification("JSON export cancelled", Severity.Info));
        }
    }

    private async Task ExportToExcelAsync(ExportSelectedRows message, string timestamp)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Selected Rows");

        // Header row
        for (var i = 0; i < message.Columns.Count; i++)
        {
            worksheet.Cell(1, i + 1).Value = message.Columns[i];
        }

        // Data rows
        for (var rowIndex = 0; rowIndex < message.Rows.Count; rowIndex++)
        {
            var row = message.Rows[rowIndex];
            for (var colIndex = 0; colIndex < message.Columns.Count; colIndex++)
            {
                var colName = message.Columns[colIndex];
                var value = row.TryGetValue(colName, out var val) ? val : null;
                worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value?.ToString() ?? "";
            }
        }

        worksheet.Columns().AdjustToContents();

        var fileName = $"selected_rows_{timestamp}.xlsx";
        var location = await _saveService.PromptFileSaveAsync(fileName);

        if (string.IsNullOrWhiteSpace(location))
        {
            await _bus.PublishAsync(new AddNotification("Excel export cancelled", Severity.Info));
            return;
        }

        workbook.SaveAs(location);
        _logger.LogInformation("Exported {Count} rows to Excel: {FileName}", message.Rows.Count, fileName);
        await _bus.PublishAsync(new AddNotification($"Exported {message.Rows.Count} rows to {fileName}", Severity.Success));
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
