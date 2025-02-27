using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Consumers;

public class QueryPersister : IConsumer<SaveQuery>, IConsumer<SaveAllQueries>
{
    private readonly IQuerySaveService _querySaveService;
    private readonly QueryState _state;
    private readonly ILogger<QueryPersister> _logger;

    public QueryPersister(IQuerySaveService querySaveService, QueryState state, ILogger<QueryPersister> logger)
    {
        _querySaveService = querySaveService;
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
        
        await _querySaveService.SaveQueryAsync(query);
    }

    public async Task Consume(SaveAllQueries message)
    {
        var tasks = _state.Queries.Select(q => _querySaveService.SaveQueryAsync(q));
        await Task.WhenAll(tasks);
    }
}