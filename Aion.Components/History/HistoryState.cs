using Aion.Components.Querying;

namespace Aion.Components.History;

public class HistoryState
{
    public event Action? HistoryStateChanged;
    public void RaiseHistoryStateChanged() => HistoryStateChanged?.Invoke();

    public List<HistoryRecord<QueryModel>> Queries { get; } = [];

    public void AddQuery(QueryModel query)
    {
        Queries.Add(new HistoryRecord<QueryModel>(query));
        RaiseHistoryStateChanged();
    }
}