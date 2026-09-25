using Aion.Contracts.Database;

namespace Aion.Contracts.Queries.Editing;

public interface ISqlChangeGenerator
{
    Task<SqlGenerationResult> GenerateSqlAsync(
        EditableQueryResult result,
        IEnumerable<PendingChange> changes,
        IStandardDatabaseCommands commands);
}
