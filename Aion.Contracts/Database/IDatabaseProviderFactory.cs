namespace Aion.Contracts.Database;

public interface IDatabaseProviderFactory
{
    IDatabaseProvider GetProvider(DatabaseType type);
    IEnumerable<DatabaseType> SupportedDatabases { get; }
}
