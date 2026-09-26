using Aion.Components.History;

namespace Aion.Test.TestDoubles;

public class InMemoryQueryHistoryStore : IQueryHistoryStore
{
    public List<QueryHistoryEntry> Saved { get; set; } = [];

    public int SaveCount { get; private set; }

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public Task<IReadOnlyList<QueryHistoryEntry>> LoadAsync()
    {
        if (LoadException is not null) throw LoadException;
        return Task.FromResult<IReadOnlyList<QueryHistoryEntry>>(Saved.ToList());
    }

    public Task SaveAsync(IReadOnlyList<QueryHistoryEntry> entries)
    {
        if (SaveException is not null) throw SaveException;
        Saved = entries.ToList();
        SaveCount++;
        return Task.CompletedTask;
    }
}
