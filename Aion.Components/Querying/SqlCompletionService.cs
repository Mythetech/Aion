using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;

namespace Aion.Components.Querying;

internal enum SqlContext
{
    Unknown,
    TablePosition,
    ColumnPosition,
    DotQualified
}

public class SqlCompletionService
{
    private readonly ConnectionState _connectionState;

    private static readonly string[] SqlKeywords =
    [
        "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET",
        "DELETE", "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW",
        "JOIN", "INNER", "LEFT", "RIGHT", "CROSS", "OUTER", "ON",
        "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL",
        "ORDER", "BY", "GROUP", "HAVING", "LIMIT", "OFFSET", "AS",
        "DISTINCT", "COUNT", "SUM", "AVG", "MIN", "MAX",
        "CASE", "WHEN", "THEN", "ELSE", "END",
        "UNION", "ALL", "INTERSECT", "EXCEPT",
        "ASC", "DESC", "NULLS", "FIRST", "LAST",
        "BEGIN", "COMMIT", "ROLLBACK", "TRUNCATE",
        "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "CONSTRAINT",
        "DEFAULT", "CHECK", "UNIQUE", "CASCADE"
    ];

    private static readonly string[] TableKeywords =
        ["FROM", "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "CROSS JOIN",
         "INTO", "UPDATE", "TABLE", "TRUNCATE"];

    private static readonly string[] ColumnKeywords =
        ["SELECT", "WHERE", "AND", "OR", "ON", "SET", "ORDER BY", "GROUP BY", "HAVING"];

    public SqlCompletionService(ConnectionState connectionState)
    {
        _connectionState = connectionState;
    }

    public async Task<List<SqlCompletionItem>> GetCompletionsAsync(
        string editorText,
        int line,
        int column,
        string? triggerCharacter,
        Guid? connectionId,
        string? databaseName)
    {
        var (connection, database) = ResolveSchema(connectionId, databaseName);
        var context = DetectContext(editorText, line, column, triggerCharacter);
        var results = new List<SqlCompletionItem>();

        if (connection != null && database != null)
        {
            await EnsureSchemaLoadedAsync(connection, database, editorText);
        }

        if (context == SqlContext.DotQualified && database != null)
        {
            var dotResults = GetDotQualifiedCompletions(editorText, line, column, database);
            if (dotResults.Count > 0) return dotResults;
        }

        if (context == SqlContext.TablePosition && database?.TablesLoaded == true)
        {
            results.AddRange(GetTableCompletions(database));
        }

        if (context == SqlContext.ColumnPosition && database != null)
        {
            var tableRefs = ExtractTableReferences(editorText, database);
            results.AddRange(GetColumnCompletions(tableRefs, database));
        }

        if (database?.RoutinesLoaded == true && context is SqlContext.ColumnPosition or SqlContext.Unknown)
        {
            results.AddRange(GetRoutineCompletions(database));
        }

        results.AddRange(GetKeywordCompletions());

        return results.Count > 500 ? results.Take(500).ToList() : results;
    }

    private (ConnectionModel? connection, DatabaseModel? database) ResolveSchema(
        Guid? connectionId, string? databaseName)
    {
        if (connectionId == null || databaseName == null) return (null, null);

        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == connectionId);
        var database = connection?.Databases.FirstOrDefault(d => d.Name == databaseName);
        return (connection, database);
    }

    private async Task EnsureSchemaLoadedAsync(
        ConnectionModel connection, DatabaseModel database, string editorText)
    {
        if (!database.TablesLoaded)
        {
            try { await _connectionState.LoadTablesAsync(connection, database); }
            catch { /* non-blocking */ }
        }

        var tableRefs = ExtractTableReferences(editorText, database);
        foreach (var (schema, table, _) in tableRefs)
        {
            var key = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
            if (database.LoadedColumnTables.Contains(key)) continue;

            try { await _connectionState.LoadColumnsAsync(connection, database, schema, table); }
            catch { /* non-blocking */ }
        }
    }

    internal static SqlContext DetectContext(string text, int line, int column, string? triggerCharacter)
    {
        var textUpToCursor = GetTextUpToCursor(text, line, column);
        if (string.IsNullOrWhiteSpace(textUpToCursor)) return SqlContext.Unknown;

        if (triggerCharacter == ".") return SqlContext.DotQualified;

        var precedingKeyword = GetPrecedingKeyword(textUpToCursor);

        return precedingKeyword switch
        {
            "FROM" or "JOIN" or "INNER JOIN" or "LEFT JOIN" or "RIGHT JOIN"
                or "CROSS JOIN" or "INTO" or "UPDATE" or "TABLE" or "TRUNCATE" => SqlContext.TablePosition,
            "SELECT" or "WHERE" or "AND" or "OR" or "ON" or "SET"
                or "ORDER BY" or "GROUP BY" or "HAVING" => SqlContext.ColumnPosition,
            _ => SqlContext.Unknown
        };
    }

    internal static string GetTextUpToCursor(string text, int line, int column)
    {
        var lines = text.Split('\n');
        if (line < 0 || line >= lines.Length) return text;

        var result = new List<string>();
        for (var i = 0; i < line; i++)
            result.Add(lines[i]);

        var lastLine = lines[line];
        var clampedCol = Math.Min(column, lastLine.Length);
        result.Add(lastLine[..clampedCol]);
        return string.Join('\n', result);
    }

    internal static string? GetPrecedingKeyword(string textUpToCursor)
    {
        var trimmed = textUpToCursor.TrimEnd();

        var allKeywords = TableKeywords.Concat(ColumnKeywords)
            .OrderByDescending(k => k.Length)
            .ToArray();

        foreach (var keyword in allKeywords)
        {
            if (trimmed.EndsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return keyword;
        }

        if (trimmed.EndsWith(','))
        {
            var upper = trimmed.ToUpperInvariant();
            var lastFrom = upper.LastIndexOf("FROM", StringComparison.Ordinal);
            var lastSelect = upper.LastIndexOf("SELECT", StringComparison.Ordinal);
            var lastJoin = upper.LastIndexOf("JOIN", StringComparison.Ordinal);

            if (lastFrom > lastSelect && lastFrom > lastJoin) return "FROM";
            if (lastSelect >= 0) return "SELECT";
        }

        return null;
    }

    internal static List<(string schema, string table, string? alias)> ExtractTableReferences(
        string text, DatabaseModel database)
    {
        var refs = new List<(string schema, string table, string? alias)>();
        if (database.Tables == null) return refs;
        var upper = text.ToUpperInvariant();

        var patterns = new[] { "FROM", "JOIN", "INTO", "UPDATE" };
        foreach (var pattern in patterns)
        {
            var searchPos = 0;
            while (true)
            {
                var idx = upper.IndexOf(pattern, searchPos, StringComparison.Ordinal);
                if (idx < 0) break;
                searchPos = idx + pattern.Length;

                var afterKeyword = text[searchPos..].TrimStart();
                var tableRef = ParseTableReference(afterKeyword, database);
                if (tableRef != null) refs.Add(tableRef.Value);
            }
        }

        return refs;
    }

    private static (string schema, string table, string? alias)? ParseTableReference(
        string text, DatabaseModel database)
    {
        var parts = text.Split([' ', '\t', '\n', '\r', ',', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var tablePart = parts[0];
        string schema;
        string table;

        if (tablePart.Contains('.'))
        {
            var dotParts = tablePart.Split('.');
            schema = dotParts[0];
            table = dotParts[1];
        }
        else
        {
            var match = database.Tables.FirstOrDefault(t =>
                t.Name.Equals(tablePart, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            schema = match.Schema;
            table = match.Name;
        }

        string? alias = null;
        if (parts.Length >= 2)
        {
            var next = parts[1].ToUpperInvariant();
            if (next == "AS" && parts.Length >= 3)
                alias = parts[2];
            else if (!IsKeyword(next))
                alias = parts[1];
        }

        return (schema, table, alias);
    }

    private static bool IsKeyword(string word)
    {
        return SqlKeywords.Contains(word, StringComparer.OrdinalIgnoreCase) ||
               word is "ON" or "WHERE" or "SET" or "INNER" or "LEFT" or "RIGHT" or "CROSS" or "OUTER";
    }

    private static List<SqlCompletionItem> GetTableCompletions(DatabaseModel database)
    {
        if (database.Tables == null || database.Tables.Count == 0) return [];

        var schemas = database.Tables.Select(t => t.Schema).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        var useSchemaPrefix = schemas.Count > 1;

        return database.Tables.Select(table =>
        {
            var label = useSchemaPrefix ? table.DisplayName : table.Name;
            return new SqlCompletionItem
            {
                Label = label,
                Kind = SqlCompletionKind.Table,
                Detail = useSchemaPrefix ? null : table.DisplayName,
                InsertText = label,
                FilterText = table.Name,
                SortText = $"1_{table.Name}"
            };
        }).ToList();
    }

    private static List<SqlCompletionItem> GetColumnCompletions(
        List<(string schema, string table, string? alias)> tableRefs,
        DatabaseModel database)
    {
        var results = new List<SqlCompletionItem>();
        if (database.TableColumns == null) return results;

        foreach (var (schema, table, _) in tableRefs)
        {
            var key = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
            if (!database.TableColumns.TryGetValue(key, out var columns) || columns == null) continue;

            foreach (var col in columns)
            {
                results.Add(new SqlCompletionItem
                {
                    Label = col.Name,
                    Kind = SqlCompletionKind.Column,
                    Detail = FormatColumnDetail(col),
                    InsertText = col.Name,
                    SortText = $"2_{col.Name}"
                });
            }
        }

        return results;
    }

    private List<SqlCompletionItem> GetDotQualifiedCompletions(
        string text, int line, int column, DatabaseModel database)
    {
        var textUpToCursor = GetTextUpToCursor(text, line, column);
        var wordBeforeDot = GetWordBeforeDot(textUpToCursor);
        if (string.IsNullOrEmpty(wordBeforeDot)) return [];

        if (database.Tables == null || database.Tables.Count == 0) return [];

        var schemas = database.Tables.Select(t => t.Schema).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        if (schemas.Any(s => s.Equals(wordBeforeDot, StringComparison.OrdinalIgnoreCase)))
        {
            return database.Tables
                .Where(t => t.Schema.Equals(wordBeforeDot, StringComparison.OrdinalIgnoreCase))
                .Select(t => new SqlCompletionItem
                {
                    Label = t.Name,
                    Kind = SqlCompletionKind.Table,
                    Detail = t.DisplayName,
                    InsertText = t.Name,
                    SortText = $"1_{t.Name}"
                }).ToList();
        }

        var tableRefs = ExtractTableReferences(text, database);
        var matchedRef = tableRefs.FirstOrDefault(r =>
            r.table.Equals(wordBeforeDot, StringComparison.OrdinalIgnoreCase) ||
            (r.alias != null && r.alias.Equals(wordBeforeDot, StringComparison.OrdinalIgnoreCase)));

        if (matchedRef != default)
        {
            var key = string.IsNullOrEmpty(matchedRef.schema)
                ? matchedRef.table
                : $"{matchedRef.schema}.{matchedRef.table}";
            if (database.TableColumns != null && database.TableColumns.TryGetValue(key, out var columns))
            {
                return columns.Select(col => new SqlCompletionItem
                {
                    Label = col.Name,
                    Kind = SqlCompletionKind.Column,
                    Detail = FormatColumnDetail(col),
                    InsertText = col.Name,
                    SortText = $"2_{col.Name}"
                }).ToList();
            }
        }

        return [];
    }

    private static string? GetWordBeforeDot(string textUpToCursor)
    {
        if (textUpToCursor.Length < 2 || textUpToCursor[^1] != '.') return null;

        var beforeDot = textUpToCursor[..^1];
        var lastSpace = beforeDot.LastIndexOfAny([' ', '\t', '\n', '\r', ',', '(', ')']);
        return lastSpace < 0 ? beforeDot : beforeDot[(lastSpace + 1)..];
    }

    private static List<SqlCompletionItem> GetRoutineCompletions(DatabaseModel database)
    {
        return database.Routines.Select(routine =>
        {
            var detail = routine.Kind == RoutineKind.Function && routine.ReturnType != null
                ? $"returns {routine.ReturnType}"
                : routine.ArgumentSignature;

            return new SqlCompletionItem
            {
                Label = routine.Name,
                Kind = routine.Kind == RoutineKind.Function ? SqlCompletionKind.Function : SqlCompletionKind.Procedure,
                Detail = detail,
                InsertText = routine.Name,
                SortText = $"3_{routine.Name}"
            };
        }).ToList();
    }

    private static string FormatColumnDetail(ColumnInfo col)
    {
        var parts = new List<string> { col.DataType };
        if (col.IsNullable) parts.Add("nullable");
        if (col.IsPrimaryKey) parts.Add("PK");
        if (col.IsForeignKey) parts.Add("FK");
        if (col.MaxLength.HasValue) parts.Add($"max: {col.MaxLength}");
        return string.Join(", ", parts);
    }

    private static List<SqlCompletionItem> GetKeywordCompletions()
    {
        return SqlKeywords.Select(kw => new SqlCompletionItem
        {
            Label = kw,
            Kind = SqlCompletionKind.Keyword,
            Detail = "SQL",
            InsertText = kw,
            SortText = $"4_{kw}"
        }).ToList();
    }
}
