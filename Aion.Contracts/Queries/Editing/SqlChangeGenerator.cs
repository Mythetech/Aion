using Aion.Contracts.Database;

namespace Aion.Contracts.Queries.Editing;

/// <summary>
/// Turns pending grid edits into statements. It only decides which columns and key values each change needs;
/// the provider's <see cref="IStandardDatabaseCommands"/> owns every piece of SQL text, including the WHERE clause.
/// </summary>
public class SqlChangeGenerator : ISqlChangeGenerator
{
    public async Task<SqlGenerationResult> GenerateSqlAsync(
        EditableQueryResult result,
        IEnumerable<PendingChange> changes,
        IStandardDatabaseCommands commands)
    {
        var changeList = changes.ToList();

        if (changeList.Count == 0)
        {
            return new SqlGenerationResult([], false);
        }

        if (string.IsNullOrEmpty(result.SourceTable))
        {
            return new SqlGenerationResult([], false, "Source table is not specified");
        }

        if (string.IsNullOrEmpty(result.SourceDatabase))
        {
            return new SqlGenerationResult([], false, "Source database is not specified");
        }

        var needsPrimaryKey = changeList.Any(c => c.Type is ChangeType.Update or ChangeType.Delete);
        if (needsPrimaryKey && !result.HasPrimaryKey)
        {
            return new SqlGenerationResult([], false, "Cannot generate UPDATE/DELETE statements without primary key columns");
        }

        var missingKeys = result.MissingPrimaryKeyColumns;
        if (needsPrimaryKey && missingKeys.Count > 0)
        {
            return new SqlGenerationResult([], false,
                $"The results must include the primary key column(s) {string.Join(", ", missingKeys)} to update or delete rows");
        }

        var statements = new List<GeneratedStatement>();
        var errors = new List<string>();

        foreach (var change in changeList)
        {
            try
            {
                var sql = change.Type switch
                {
                    ChangeType.Insert => await GenerateInsertAsync(result, change, commands),
                    ChangeType.Update => await GenerateUpdateAsync(result, change, commands),
                    ChangeType.Delete => await GenerateDeleteAsync(result, change, commands),
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (!string.IsNullOrEmpty(sql))
                {
                    statements.Add(new GeneratedStatement(change, sql));
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to generate SQL for {change.Type} at row {change.RowIndex + 1}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return new SqlGenerationResult(statements, true, string.Join("; ", errors));
        }

        var requiresTransaction = statements.Count > 1;

        return new SqlGenerationResult(statements, requiresTransaction);
    }

    private static async Task<string> GenerateInsertAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        if (change.NewValues == null || change.NewValues.Count == 0)
        {
            throw new InvalidOperationException("Insert change has no values");
        }

        var valuesToInsert = change.NewValues
            .Select(kvp => (Info: ResolveColumn(result, kvp.Key), kvp.Value))
            .Where(c => !c.Info.IsIdentity)
            .Select(c => new ColumnValue(c.Info.Name, c.Value))
            .ToList();

        return await commands.GenerateInsertScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            valuesToInsert);
    }

    private static async Task<string> GenerateUpdateAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        if (change.NewValues == null)
        {
            throw new InvalidOperationException("Update change has no new values");
        }

        var valuesToUpdate = change.GetModifiedColumns()
            .Select(col => (Info: ResolveColumn(result, col), Value: change.NewValues.GetValueOrDefault(col)))
            .Where(c => !c.Info.IsPrimaryKey && !c.Info.IsIdentity)
            .Select(c => new ColumnValue(c.Info.Name, c.Value))
            .ToList();

        if (valuesToUpdate.Count == 0)
        {
            return string.Empty;
        }

        return await commands.GenerateUpdateScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            valuesToUpdate,
            GetKeyValues(result, change));
    }

    private static async Task<string> GenerateDeleteAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        return await commands.GenerateDeleteScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            GetKeyValues(result, change));
    }

    private static ColumnInfo ResolveColumn(EditableQueryResult result, string column)
    {
        return result.GetColumnInfo(column)
            ?? throw new InvalidOperationException($"'{column}' is not a column of {result.SourceTable}");
    }

    private static List<ColumnValue> GetKeyValues(EditableQueryResult result, PendingChange change)
    {
        var keyValues = new List<ColumnValue>();

        foreach (var keyColumn in result.PrimaryKeyColumns)
        {
            if (!TryGetOriginalValue(change, keyColumn, out var value))
            {
                throw new InvalidOperationException($"The row has no value for primary key column '{keyColumn}'");
            }

            // A NULL key would become "IS NULL", which can match many rows in engines that allow it.
            if (value is null or DBNull)
            {
                throw new InvalidOperationException($"Primary key column '{keyColumn}' is NULL, so the row cannot be targeted safely");
            }

            keyValues.Add(new ColumnValue(keyColumn, value));
        }

        return keyValues;
    }

    private static bool TryGetOriginalValue(PendingChange change, string column, out object? value)
    {
        if (change.OriginalValues.TryGetValue(column, out value))
        {
            return true;
        }

        foreach (var (key, candidate) in change.OriginalValues)
        {
            if (key.Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }
}
