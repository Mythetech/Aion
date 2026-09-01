namespace Aion.Contracts.Metrics;

public interface IDatabaseStorageMetrics
{
    Task<DatabaseSizeInfo> GetDatabaseSizeAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TableSizeInfo>> GetTableSizesAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default);
}
