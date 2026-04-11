namespace Aion.Core.Database;

public interface IDatabaseIndexProvider
{
    Task<List<IndexInfo>> GetIndexesAsync(string connectionString, string database);
}
