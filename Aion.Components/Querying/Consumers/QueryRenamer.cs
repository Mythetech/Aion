using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Dialogs.Commands;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class QueryRenamer : IConsumer<PromptRenameQuery>, IConsumer<PromptRenameActiveQuery>, IConsumer<RenameQuery>, IConsumer<RenameActiveQuery>

{
    private readonly IMessageBus _bus;
    private readonly QueryState _state;

    public QueryRenamer(IMessageBus bus, QueryState state)
    {
        _bus = bus;
        _state = state;
    }
    public async Task Consume(PromptRenameQuery message)
    {
        var parameters = new DialogParameters { { "Query", message.Query } };

        await ShowRenameDialogAsync(message.Query.Name, parameters);
    }

    public async Task Consume(PromptRenameActiveQuery message)
    {
        var query = _state.Active;

        if (query == null) return;
        
        var parameters = new DialogParameters { { "Query", query } };

        await ShowRenameDialogAsync(query.Name, parameters);
    }
    
    private async Task ShowRenameDialogAsync(string name, DialogParameters parameters) => await _bus.PublishAsync(new ShowDialog(typeof(RenameQueryDialog), $"Rename {(string.IsNullOrWhiteSpace(name) ? "Query" : name)}", Parameters: parameters));

    public Task Consume(RenameQuery message)
    {
        _state.RenameQuery(message.Query, message.Name);
        return Task.CompletedTask;
    }

    public Task Consume(RenameActiveQuery message)
    {
        _state.RenameActiveQuery(message.Name);
        return Task.CompletedTask;
    }
}