using System.Text.Json;
using Aion.Components.Infrastructure;
using Aion.Components.Querying.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Shared.Snackbar;
using Aion.Components.Shared.Snackbar.Commands;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class JsonResultsExporter : IConsumer<ExportResultsToJson>
{
    private readonly QueryState _state;
    private readonly ILogger<JsonResultsExporter> _logger;
    private readonly IMessageBus _bus;
    private readonly IFileSaveService _saveService;

    public JsonResultsExporter(QueryState state, ILogger<JsonResultsExporter> logger, IMessageBus bus, IFileSaveService saveService)
    {
        _state = state;
        _logger = logger;
        _bus = bus;
        _saveService = saveService;
    }

    public async Task Consume(ExportResultsToJson message)
    {
        var result = message.Result ?? _state.Active?.Result;
        
        if (result == null)
        {
            _logger.LogError("No query result available to export");
            return;
        }

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(result.Rows, options);

            var fileName = $"query_results_{DateTime.Now:yyyyMMddHHmmss}.json";
            bool success = await _saveService.SaveFileAsync(fileName, json);

            if (!success)
            {
                await _bus.PublishAsync(new AddNotification($"Csv export cancelled", Severity.Info));
                return;
            }
            
            _logger.LogInformation("Exported query results to JSON: {FileName}", fileName);
            await _bus.PublishAsync(new AddNotification($"Exported {fileName} results to JSON", Severity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export query results to JSON");
        }
    }
} 