using Aion.Components.Infrastructure.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using MudBlazor;

namespace Aion.Components.Querying.Consumers;

public class QueryClipboardCopier : IConsumer<CopyQueryToClipboard>
{
    private readonly IMessageBus _bus;
    private readonly QueryState _state;

    public QueryClipboardCopier(IMessageBus bus, QueryState state)
    {
        _bus = bus;
        _state = state;
    }

    public async Task Consume(CopyQueryToClipboard message)
    {
        var query = message.Query ?? _state.Active;

        if (query == null) return;

        await _bus.PublishAsync(new CopyToClipboard(query.Query));
    }
}