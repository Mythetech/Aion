using Aion.Components.Infrastructure;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class QueryFileExporter : IConsumer<SaveQueryAs>
{
    private readonly QueryState _state;
    private readonly IFileSaveService _saveService;
    private readonly IMessageBus _bus;

    public QueryFileExporter(QueryState state, IFileSaveService saveService, IMessageBus bus)
    {
        _state = state;
        _saveService = saveService;
        _bus = bus;
    }
    
    public async Task Consume(SaveQueryAs message)
    {
        var query = message.Query ?? _state.Active;

        if (query == null)
        {
            await _bus.PublishAsync(new AddNotification("No query to export", Severity.Warning));
            return;
        }

        var success = await _saveService.SaveFileAsync(query.Name + ".sql", query.Query);

        var notification = new AddNotification(
        
            success ? $"File saved" : "Save cancelled",
            success ? Severity.Success : Severity.Info
        );
        
        await _bus.PublishAsync(notification);
    }
}