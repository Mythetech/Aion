using System.Globalization;
using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding.DataGeneration;

/// <summary>
/// Decides how each column of a table is filled: left to the database, or which generator makes its values
/// and within what limits. Everything here is decided from the column's metadata before any SQL runs.
/// </summary>
public static class DataGenerationPlan
{
    public const int MaxRows = 10_000;

    private const int DefaultMaxValue = 1000;
    private const int DefaultMinLength = 5;
    private const int DefaultMaxLength = 20;

    public static List<ColumnGeneratorBinding> CreateBindings(IReadOnlyList<ColumnInfo> columns, DatabaseType engine) =>
        columns.Select(column =>
        {
            var type = ColumnTypeShape.Of(column, engine);
            var binding = new ColumnGeneratorBinding
            {
                Column = column,
                Type = type,
                FilledByDatabase = FilledByDatabase(column, type, columns, engine),
                Options = DefaultOptions(type)
            };
            binding.Generator = binding.FilledByDatabase is null ? Suggest(binding) : null;
            return binding;
        }).ToList();

    /// <summary>
    /// The generators whose values the column can hold. NULL is offered only when the column allows it and
    /// existing references only for a foreign key. Leaving the column out is always offered, because providers
    /// don't report computed columns and those accept no value at all.
    /// </summary>
    public static IReadOnlyList<IDataGenerator> CompatibleGenerators(ColumnGeneratorBinding binding) =>
        binding.FilledByDatabase is not null
            ? []
            : DataGenerators.All.Where(generator => generator switch
            {
                NullGenerator => binding.Column.IsNullable,
                ReferencedValueGenerator => binding.Column.ForeignKey is not null,
                _ => generator.Supports(binding.Type)
            }).ToList();

    /// <summary>What stops the rows from being generated, worded as what to change.</summary>
    public static IReadOnlyList<string> Problems(DataGenerationModel model)
    {
        var problems = new List<string>();

        if (model.RowCount is < 1 or > MaxRows)
            problems.Add($"Choose between 1 and {MaxRows.ToString("N0", CultureInfo.InvariantCulture)} rows");

        if (model.ColumnGenerators.All(b => b.FilledByDatabase is not null))
        {
            problems.Add("The database fills in every column of this table, so there is nothing to generate");
            return problems;
        }

        var problemsBeforeColumns = problems.Count;
        foreach (var binding in model.ColumnGenerators.Where(b => b.FilledByDatabase is null))
        {
            var column = binding.Column;
            switch (binding.Generator)
            {
                case null when !column.IsNullable && column.DefaultValue is null:
                    problems.Add($"Pick a generator for \"{column.Name}\": it can't be NULL and has no default");
                    break;
                case NullGenerator when !column.IsNullable:
                    problems.Add($"\"{column.Name}\" can't be NULL");
                    break;
                case CustomListGenerator when CustomListGenerator.Values(binding.Options).Length == 0:
                    problems.Add($"Enter the values to pick from for \"{column.Name}\"");
                    break;
            }
        }

        // A row needs at least one value to insert; the per-column problems above already say which to pick.
        if (problems.Count == problemsBeforeColumns && !model.ColumnGenerators.Any(b => b.WritesValue))
            problems.Add("Choose a generator for at least one column");

        return problems;
    }

    /// <summary>
    /// Why the database writes the column itself, or null when Aion generates its values. Values sent for these
    /// columns are rejected (identity, rowversion) or would collide with the numbers the database hands out.
    /// </summary>
    private static string? FilledByDatabase(ColumnInfo column, ColumnTypeShape type, IReadOnlyList<ColumnInfo> columns, DatabaseType engine)
    {
        if (column.IsIdentity)
            return "identity";

        if (type.Family == ColumnTypeFamily.RowVersion)
            return "rowversion";

        // Only a lone key declared exactly INTEGER becomes SQLite's rowid, which numbers new rows by itself.
        if (engine == DatabaseType.WasmSQLite
            && column.IsPrimaryKey
            && columns.Count(c => c.IsPrimaryKey) == 1
            && string.Equals(column.DataType.Trim(), "INTEGER", StringComparison.OrdinalIgnoreCase))
            return "rowid";

        return null;
    }

    private static DataGeneratorOptions DefaultOptions(ColumnTypeShape type)
    {
        var options = new DataGeneratorOptions();

        if (type.Family is ColumnTypeFamily.Integer or ColumnTypeFamily.Decimal or ColumnTypeFamily.Float)
        {
            long from = 0, to = DefaultMaxValue;
            if (type.MinInteger is { } min && type.MaxInteger is { } max)
            {
                from = Math.Clamp(0, Math.Max(min, int.MinValue), Math.Min(max, int.MaxValue));
                to = Math.Min(max, from + DefaultMaxValue);
            }

            options.MinValue = (int)from;
            options.MaxValue = (int)to;
        }

        if (type.MaxLength is { } length)
        {
            options.MinLength = Math.Min(DefaultMinLength, length);
            options.MaxLength = Math.Min(DefaultMaxLength, length);
        }

        return options;
    }

    private static IDataGenerator? Suggest(ColumnGeneratorBinding binding)
    {
        var column = binding.Column;
        var compatible = CompatibleGenerators(binding);

        IDataGenerator? Offered<T>() where T : IDataGenerator => compatible.OfType<T>().Cast<IDataGenerator>().FirstOrDefault();

        if (column.ForeignKey is not null)
            return Offered<ReferencedValueGenerator>();

        var name = column.Name.ToLowerInvariant();

        if (name.Contains("email") || name.Contains("e_mail"))
            return Offered<EmailGenerator>() ?? ByType();

        if (name.Contains("name"))
            return Offered<NameGenerator>() ?? ByType();

        if (name.Contains("uuid") || name.Contains("guid"))
            return Offered<UuidGenerator>() ?? ByType();

        if (IsDateLike(name) || IsDateType(binding.Type))
            return Offered<DateRangeGenerator>() ?? ByType();

        if (name.StartsWith("is_") || name.StartsWith("has_") || name.Contains("active") || name.Contains("enabled"))
            return Offered<BooleanGenerator>() ?? ByType();

        if (column.IsPrimaryKey)
            return Offered<AutoIncrementGenerator>() ?? ByType();

        return ByType();

        IDataGenerator? ByType() => binding.Type.Family switch
        {
            ColumnTypeFamily.Integer => Offered<RandomIntGenerator>(),
            ColumnTypeFamily.Decimal or ColumnTypeFamily.Float => Offered<RandomNumberGenerator>(),
            ColumnTypeFamily.Boolean => Offered<BooleanGenerator>(),
            ColumnTypeFamily.Text => Offered<RandomTextGenerator>(),
            ColumnTypeFamily.Uuid => Offered<UuidGenerator>(),
            ColumnTypeFamily.Json => Offered<JsonGenerator>(),
            _ => null
        } ?? (column.DefaultValue is not null ? Offered<DatabaseDefaultGenerator>() : Offered<NullGenerator>());
    }

    private static bool IsDateLike(string name) =>
        name.Contains("date") || name.Contains("timestamp") || name.Contains("created") || name.Contains("updated")
        || name.EndsWith("_at") || name.EndsWith("_on") || name.EndsWith("time");

    private static bool IsDateType(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Date or ColumnTypeFamily.DateTime
        or ColumnTypeFamily.DateTimeOffset or ColumnTypeFamily.Time;
}
