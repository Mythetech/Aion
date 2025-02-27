using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying.Commands;

namespace Aion.Components.Querying.Consumers;

public class PersistedQueryDeleter : IConsumer<DeleteQuery>
{
    private readonly IQuerySaveService _querySaveService;

    public PersistedQueryDeleter(IQuerySaveService querySaveService)
    {
        _querySaveService = querySaveService;
    }

    public async Task Consume(DeleteQuery message)
    {
        await _querySaveService.DeleteQueryAsync(message.Query);
    }
}