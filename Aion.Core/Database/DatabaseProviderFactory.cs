namespace Aion.Core.Database;

public class DatabaseProviderFactory : IDatabaseProviderFactory
{
    private readonly IEnumerable<IDatabaseProvider> _providers;
    private readonly Dictionary<DatabaseType, IDatabaseProvider> _providerMap;

    public DatabaseProviderFactory(IEnumerable<IDatabaseProvider> providers)
    {
        _providers = providers;
        _providerMap = _providers.ToDictionary(p => p.DatabaseType);
    }

    public IDatabaseProvider GetProvider(DatabaseType type)
    {
        if (!_providerMap.TryGetValue(type, out var provider))
        {
            throw new NotSupportedException($"Database type {type} is not supported");
        }
        return provider;
    }

    public IEnumerable<DatabaseType> SupportedDatabases => _providerMap.Keys;
} 