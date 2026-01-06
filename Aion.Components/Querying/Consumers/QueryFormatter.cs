using Aion.Components.Infrastructure;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using SQL.Formatter;

namespace Aion.Components.Querying.Consumers;

public class QueryFormatter : IConsumer<FormatQuery>
{
    private readonly QueryState _state;
    private readonly IFileSaveService _saveService;
    private readonly IMessageBus _bus;
    
    public QueryFormatter(QueryState state, IFileSaveService saveService, IMessageBus bus)
    {
        _state = state;
        _saveService = saveService;
        _bus = bus;
    }
    public async Task Consume(FormatQuery message)
    {
        var query = message.Query ?? _state.Active;

        var formatted = SqlFormatter.Format(query.Query);
        await _state.UpdateQueryText(query, formatted);
    }
}