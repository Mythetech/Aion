namespace Aion.Contracts.Database;

/// <summary>
/// An engine that has views. They are listed as <see cref="TableInfo"/> because they are read like tables:
/// their columns load through <see cref="IDatabaseProvider.GetColumnsAsync"/> and their rows through a SELECT.
/// </summary>
public interface IDatabaseViewProvider
{
    Task<List<TableInfo>> GetViewsAsync(string connectionString, string database);
}
