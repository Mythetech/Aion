using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Consumers;

public class QueryPersister : IConsumer<SaveQuery>, IConsumer<SaveAllQueries>
{
    private readonly QueryState _state;
    private readonly ILogger<QueryPersister> _logger;

    public QueryPersister(QueryState state, ILogger<QueryPersister> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task Consume(SaveQuery message)
    {
        var query = message.Query ?? _state.Active;

        if (query == null)
        {
            _logger.LogWarning("No active query found.");
            return;
        }

        await _state.SaveAsync(query);
    }

    public async Task Consume(SaveAllQueries message)
    {
        await _state.SaveAllAsync();
    }
}
