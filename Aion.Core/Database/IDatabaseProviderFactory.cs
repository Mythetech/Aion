namespace Aion.Core.Database;

public interface IDatabaseProviderFactory
{
    IDatabaseProvider GetProvider(DatabaseType type);
    IEnumerable<DatabaseType> SupportedDatabases { get; }
} 