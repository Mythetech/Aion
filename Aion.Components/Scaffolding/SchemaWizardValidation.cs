using System.Text;
using System.Text.RegularExpressions;
using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding;

/// <summary>
/// Everything that stops the schema wizard's definition from being created, worded as what the user has to
/// do. The wizard lists these beside its Finish button, so each one names the table or column it is about.
/// </summary>
public static partial class SchemaWizardValidation
{
    // PostgreSQL silently truncates longer identifiers (NAMEDATALEN - 1), so the table would get a different name.
    private const int PostgresIdentifierBytes = 63;

    public static IReadOnlyList<string> Problems(SchemaWizardModel model, IEnumerable<string> existingDatabaseNames)
    {
        var problems = new List<string>();

        if (DatabaseProblem(model.DatabaseName, existingDatabaseNames) is { } databaseProblem)
            problems.Add(databaseProblem);

        for (var index = 0; index < model.Tables.Count; index++)
        {
            var label = model.Tables[index].Label(index);
            problems.AddRange(TableProblems(model, index).Select(problem => $"{label}: {problem}"));
        }

        return problems;
    }

    private static string? DatabaseProblem(string name, IEnumerable<string> existingDatabaseNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Enter a database name";

        if (name != name.Trim())
            return "The database name can't start or end with a space";

        // The name becomes part of a connection string and a browser storage path, where ; = / and \ have meaning.
        if (!DatabaseNameCharacters().IsMatch(name))
            return "The database name can only use letters, numbers, spaces, hyphens and underscores";

        // In-browser databases are remembered by name alone, so the same name on another engine would collide too.
        if (existingDatabaseNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return $"A database named \"{name}\" already exists";

        return null;
    }

    private static IEnumerable<string> TableProblems(SchemaWizardModel model, int index)
    {
        var table = model.Tables[index];
        var engine = model.EngineType;

        if (string.IsNullOrWhiteSpace(table.Name))
        {
            yield return "Enter a table name";
        }
        else
        {
            if (NameProblem(table.Name, engine) is { } nameProblem)
                yield return nameProblem;

            if (model.Tables.Take(index).Any(other => SameName(other.Name, table.Name)))
                yield return $"Another table is also named \"{table.Name}\"";
        }

        if (table.Columns.Count == 0)
        {
            yield return "Add at least one column";
            yield break;
        }

        for (var c = 0; c < table.Columns.Count; c++)
        {
            var column = table.Columns[c];
            var named = !string.IsNullOrWhiteSpace(column.Name);

            if (!named)
                yield return $"Column {c + 1} needs a name";
            else if (NameProblem(column.Name, engine) is { } nameProblem)
                yield return $"Column \"{column.Name}\": {nameProblem}";

            if (string.IsNullOrWhiteSpace(column.DataType))
                yield return named ? $"Column \"{column.Name}\" needs a data type" : $"Column {c + 1} needs a data type";
        }

        var duplicates = table.Columns
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in duplicates)
            yield return $"More than one column is named \"{duplicate.First().Name}\"";

        // The generated CREATE TABLE marks each key column PRIMARY KEY on its own line, which engines reject for more than one.
        if (table.Columns.Count(c => c.IsPrimaryKey) > 1)
            yield return "Only one column can be the primary key";
    }

    private static string? NameProblem(string name, DatabaseType engine)
    {
        if (name != name.Trim())
            return "Names can't start or end with a space";

        if (name.Any(char.IsControl))
            return "Names can't contain control characters";

        if (engine == DatabaseType.WasmSQLite && name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase))
            return "Names starting with sqlite_ are reserved by SQLite";

        if (engine is DatabaseType.WasmPostgreSQL or DatabaseType.PostgreSQL && Encoding.UTF8.GetByteCount(name) > PostgresIdentifierBytes)
            return $"PostgreSQL names can be at most {PostgresIdentifierBytes} bytes long";

        return null;
    }

    private static bool SameName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[\p{L}\p{N} _-]+$")]
    private static partial Regex DatabaseNameCharacters();
}
