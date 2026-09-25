namespace Aion.Contracts.Database;

/// <summary>
/// A provider whose databases Aion creates and stores itself, such as the in-browser engines, instead of
/// reaching them on a server. Aion is the only way to reach that storage, so it is also responsible for
/// deleting it.
/// </summary>
public interface IManagedDatabaseProvider
{
    Task EnsureDatabaseAsync(string database);

    Task DeleteDatabaseAsync(string database);
}
