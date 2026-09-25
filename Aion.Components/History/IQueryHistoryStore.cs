namespace Aion.Components.History;

/// <summary>
/// Persists query history between sessions. <see cref="HistoryState"/> owns ordering and the entry cap;
/// a store only reads and replaces the saved list.
/// </summary>
public interface IQueryHistoryStore
{
    Task<IReadOnlyList<QueryHistoryEntry>> LoadAsync();

    Task SaveAsync(IReadOnlyList<QueryHistoryEntry> entries);
}
