using Aion.Contracts.Database;

namespace Aion.Contracts.Queries.Editing;

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

        var statements = new List<string>();
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
                    statements.Add(sql);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to generate SQL for {change.Type} at row {change.RowIndex}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return new SqlGenerationResult(statements, true, string.Join("; ", errors));
        }

        var requiresTransaction = statements.Count > 1;

        return new SqlGenerationResult(statements, requiresTransaction);
    }

    private async Task<string> GenerateInsertAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        if (change.NewValues == null || change.NewValues.Count == 0)
        {
            throw new InvalidOperationException("Insert change has no values");
        }

        var valuesToInsert = change.NewValues
            .Where(kvp =>
            {
                var colInfo = result.GetColumnInfo(kvp.Key);
                return colInfo == null || !colInfo.IsIdentity;
            })
            .Select(kvp => new ColumnValue(kvp.Key, kvp.Value));

        return await commands.GenerateInsertScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            valuesToInsert);
    }

    private async Task<string> GenerateUpdateAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        if (change.NewValues == null)
        {
            throw new InvalidOperationException("Update change has no new values");
        }

        var modifiedColumns = change.GetModifiedColumns().ToList();
        if (modifiedColumns.Count == 0)
        {
            return string.Empty;
        }

        var valuesToUpdate = modifiedColumns
            .Where(col =>
            {
                var colInfo = result.GetColumnInfo(col);
                return colInfo == null || (!colInfo.IsPrimaryKey && !colInfo.IsIdentity);
            })
            .Select(col => new ColumnValue(col, change.NewValues.GetValueOrDefault(col)));

        if (!valuesToUpdate.Any())
        {
            return string.Empty;
        }

        var pkValues = new Dictionary<string, object?>();
        foreach (var pkCol in result.PrimaryKeyColumns)
        {
            pkValues[pkCol] = change.OriginalValues.GetValueOrDefault(pkCol);
        }

        var whereClause = GenerateWhereClause(pkValues, result.PrimaryKeyColumns);

        return await commands.GenerateUpdateScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            valuesToUpdate,
            whereClause);
    }

    private async Task<string> GenerateDeleteAsync(
        EditableQueryResult result,
        PendingChange change,
        IStandardDatabaseCommands commands)
    {
        var pkValues = new Dictionary<string, object?>();
        foreach (var pkCol in result.PrimaryKeyColumns)
        {
            pkValues[pkCol] = change.OriginalValues.GetValueOrDefault(pkCol);
        }

        var whereClause = GenerateWhereClause(pkValues, result.PrimaryKeyColumns);

        return await commands.GenerateDeleteScript(
            result.SourceDatabase!,
            result.SourceSchema ?? "",
            result.SourceTable!,
            whereClause);
    }

    public string GenerateWhereClause(
        Dictionary<string, object?> primaryKeyValues,
        List<string> primaryKeyColumns)
    {
        var conditions = primaryKeyColumns
            .Select(col =>
            {
                var value = primaryKeyValues.GetValueOrDefault(col);
                return FormatCondition(col, value);
            });

        return string.Join(" AND ", conditions);
    }

    private static string FormatCondition(string column, object? value)
    {
        if (value == null)
        {
            return $"\"{column}\" IS NULL";
        }

        var formattedValue = FormatValue(value);
        return $"\"{column}\" = {formattedValue}";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{EscapeString(s)}'",
            bool b => b ? "TRUE" : "FALSE",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss zzz}'",
            Guid g => $"'{g}'",
            byte[] bytes => $"E'\\\\x{BitConverter.ToString(bytes).Replace("-", "")}'",
            _ when IsNumeric(value) => value.ToString() ?? "NULL",
            _ => $"'{EscapeString(value.ToString() ?? "")}'",
        };
    }

    private static bool IsNumeric(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

    private static string EscapeString(string value)
    {
        return value.Replace("'", "''");
    }
}
