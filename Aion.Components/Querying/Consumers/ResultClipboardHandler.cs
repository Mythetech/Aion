using System.Text;
using System.Text.Json;
using Aion.Components.Infrastructure.Commands;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

/// <summary>
/// Handles clipboard operations for query result cells and rows.
/// </summary>
public class ResultClipboardHandler :
    IConsumer<CopyCellToClipboard>,
    IConsumer<CopyRowToClipboard>,
    IConsumer<CopySelectedRowsToClipboard>
{
    private readonly IMessageBus _bus;

    public ResultClipboardHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(CopyCellToClipboard message)
    {
        await _bus.PublishAsync(new CopyToClipboard(message.Value));
        await _bus.PublishAsync(new AddNotification("Cell copied to clipboard", Severity.Info));
    }

    public async Task Consume(CopyRowToClipboard message)
    {
        var text = message.Format.Equals("Json", StringComparison.OrdinalIgnoreCase)
            ? FormatRowAsJson(message.Row)
            : FormatRowAsCsv(message.Row, message.Columns);

        await _bus.PublishAsync(new CopyToClipboard(text));
        await _bus.PublishAsync(new AddNotification("Row copied to clipboard", Severity.Info));
    }

    public async Task Consume(CopySelectedRowsToClipboard message)
    {
        if (message.Rows.Count == 0)
        {
            await _bus.PublishAsync(new AddNotification("No rows selected", Severity.Warning));
            return;
        }

        var text = message.Format.Equals("Json", StringComparison.OrdinalIgnoreCase)
            ? FormatRowsAsJson(message.Rows)
            : FormatRowsAsCsv(message.Rows, message.Columns);

        await _bus.PublishAsync(new CopyToClipboard(text));
        await _bus.PublishAsync(new AddNotification($"{message.Rows.Count} row(s) copied to clipboard", Severity.Info));
    }

    private static string FormatRowAsJson(Dictionary<string, object> row)
    {
        return JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatRowsAsJson(List<Dictionary<string, object>> rows)
    {
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatRowAsCsv(Dictionary<string, object> row, List<string> columns)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", columns.Select(EscapeCsvField)));

        // Data row
        var values = columns.Select(c => row.TryGetValue(c, out var val) ? val?.ToString() ?? "" : "");
        sb.AppendLine(string.Join(",", values.Select(EscapeCsvField)));

        return sb.ToString();
    }

    private static string FormatRowsAsCsv(List<Dictionary<string, object>> rows, List<string> columns)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", columns.Select(EscapeCsvField)));

        // Data rows
        foreach (var row in rows)
        {
            var values = columns.Select(c => row.TryGetValue(c, out var val) ? val?.ToString() ?? "" : "");
            sb.AppendLine(string.Join(",", values.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // If the field contains a comma, quote, or newline, wrap it in quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            // Escape existing quotes by doubling them
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
