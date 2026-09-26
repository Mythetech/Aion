using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Querying.Errors;

/// <param name="Name">The existing table or column the misspelled name most likely meant.</param>
/// <param name="Table">For a column, the table it belongs to.</param>
public sealed record ErrorSuggestion(string Name, string? Table = null);

/// <summary>
/// Suggests the table or column an unknown-name error most likely meant, from the same schema cache
/// that SQL IntelliSense fills, loading only the tables the SQL refers to.
/// </summary>
public class QueryErrorSuggester
{
    private readonly ConnectionState _connections;
    private readonly ILogger<QueryErrorSuggester> _logger;

    public QueryErrorSuggester(ConnectionState connections, ILogger<QueryErrorSuggester> logger)
    {
        _connections = connections;
        _logger = logger;
    }

    public async Task<ErrorSuggestion?> SuggestAsync(QueryError error, string sql, Guid? connectionId, string? databaseName)
    {
        if (error.Token is not { } token || error.Kind is not (QueryErrorKind.UnknownColumn or QueryErrorKind.UnknownTable))
            return null;

        var connection = _connections.Connections.FirstOrDefault(c => c.Id == connectionId);
        var database = connection?.Databases.FirstOrDefault(d => d.Name == databaseName);
        if (connection == null || database == null) return null;

        await _connections.LoadTablesAsync(connection, database);

        var (qualifier, name) = SplitQualifier(token);
        return error.Kind == QueryErrorKind.UnknownTable
            ? SuggestTable(name, database)
            : await SuggestColumnAsync(name, qualifier, sql, connection, database);
    }

    private static ErrorSuggestion? SuggestTable(string name, DatabaseModel database)
    {
        var match = IdentifierMatcher.Closest(name, database.Tables.Select(t => t.Name).Distinct());
        return match == null ? null : new ErrorSuggestion(match);
    }

    private async Task<ErrorSuggestion?> SuggestColumnAsync(
        string name, string? qualifier, string sql, ConnectionModel connection, DatabaseModel database)
    {
        var references = SqlCompletionService.ExtractTableReferences(sql, database);
        if (qualifier != null)
        {
            var qualified = references
                .Where(r => qualifier.Equals(r.alias, StringComparison.OrdinalIgnoreCase)
                            || qualifier.Equals(r.table, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (qualified.Count > 0) references = qualified;
        }

        var columns = new List<(string Column, string Table)>();
        foreach (var (schema, table, _) in references.DistinctBy(r => (r.schema, r.table)))
        {
            var key = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
            try
            {
                await _connections.LoadColumnsAsync(connection, database, schema, table);
            }
            catch (Exception ex)
            {
                // A suggestion is a nicety; a table whose columns can't be read just isn't searched.
                _logger.LogDebug(ex, "Could not load columns of {Table} for an error suggestion", key);
                continue;
            }

            if (database.TableColumns.TryGetValue(key, out var tableColumns))
            {
                columns.AddRange(tableColumns.Select(c => (c.Name, table)));
            }
        }

        var match = IdentifierMatcher.Closest(name, columns.Select(c => c.Column).Distinct());
        return match == null ? null : new ErrorSuggestion(match, columns.First(c => c.Column == match).Table);
    }

    /// <summary>Splits "p.categry_id" into its qualifier and name, dropping identifier quotes.</summary>
    private static (string? Qualifier, string Name) SplitQualifier(string token)
    {
        var parts = token.Split('.').Select(part => part.Trim('"', '`', '[', ']')).ToArray();
        return parts.Length > 1 ? (parts[^2], parts[^1]) : (null, parts[0]);
    }
}
