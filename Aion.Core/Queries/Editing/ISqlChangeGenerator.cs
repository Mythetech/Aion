using Aion.Core.Database;

namespace Aion.Core.Queries.Editing;

/// <summary>
/// Service for generating SQL statements from pending changes.
/// </summary>
public interface ISqlChangeGenerator
{
    /// <summary>
    /// Generate SQL statements for the given pending changes.
    /// </summary>
    Task<SqlGenerationResult> GenerateSqlAsync(
        EditableQueryResult result,
        IEnumerable<PendingChange> changes,
        IStandardDatabaseCommands commands);

    /// <summary>
    /// Generate a WHERE clause from primary key values.
    /// </summary>
    string GenerateWhereClause(
        Dictionary<string, object?> primaryKeyValues,
        List<string> primaryKeyColumns);
}
