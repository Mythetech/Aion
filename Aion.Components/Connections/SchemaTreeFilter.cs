using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Narrows the schema tree to the tables and views whose names contain the filter text, ignoring case, and to
/// those with a loaded column whose name contains it. Columns are never fetched for the filter: on a server with
/// thousands of tables that would be a query per table on every keystroke.
/// </summary>
public sealed class SchemaTreeFilter
{
    public SchemaTreeFilter(string? text)
    {
        Text = text?.Trim() ?? "";
    }

    public string Text { get; }

    public bool IsActive => Text.Length > 0;

    /// <summary>
    /// The relations to list, in their original order. Without filter text that is every one of them.
    /// </summary>
    public List<SchemaTreeMatch> Apply(IEnumerable<TableInfo> relations, DatabaseModel database)
    {
        if (!IsActive)
            return relations.Select(relation => new SchemaTreeMatch(relation, null)).ToList();

        var matches = new List<SchemaTreeMatch>();
        foreach (var relation in relations)
        {
            // The display name is what the row shows, so "sales." narrows to one schema.
            if (Matches(relation.DisplayName))
            {
                matches.Add(new SchemaTreeMatch(relation, null));
                continue;
            }

            var columns = LoadedColumns(database, relation)?.Where(column => Matches(column.Name)).ToList();
            if (columns is { Count: > 0 })
                matches.Add(new SchemaTreeMatch(relation, columns));
        }

        return matches;
    }

    private bool Matches(string name) => name.Contains(Text, StringComparison.OrdinalIgnoreCase);

    private static List<ColumnInfo>? LoadedColumns(DatabaseModel database, TableInfo relation) =>
        database.LoadedColumnTables.Contains(relation.DisplayName)
            ? database.TableColumns.GetValueOrDefault(relation.DisplayName)
            : null;
}

/// <summary>
/// A table or view the filter keeps. <see cref="MatchingColumns"/> is null when its own name matched, so every
/// column shows; otherwise it holds only the columns that matched.
/// </summary>
public sealed record SchemaTreeMatch(TableInfo Table, IReadOnlyList<ColumnInfo>? MatchingColumns)
{
    public bool MatchedByColumns => MatchingColumns is not null;
}
