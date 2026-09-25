using Mythetech.Framework.Infrastructure.Initialization;

namespace Aion.Components.History;

public class QueryHistoryInitializationHook : IAsyncInitializationHook
{
    private readonly HistoryState _state;

    public QueryHistoryInitializationHook(HistoryState state)
    {
        _state = state;
    }

    public string Name => "Query history";

    public Task InitializeAsync(CancellationToken cancellationToken = default) => _state.InitializeAsync();
}
