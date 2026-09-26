using System.Globalization;

namespace Aion.Components.Scaffolding.DataGeneration;

public class AutoIncrementGenerator : IDataGenerator
{
    public string Name => "Auto Increment";
    public string Description => "Sequential numbers, after the highest one already in the column unless a start is chosen";

    public bool Supports(ColumnTypeShape type) =>
        type.Family is ColumnTypeFamily.Integer or ColumnTypeFamily.Decimal or ColumnTypeFamily.Float;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var start = options.StartValue ?? (options.ExistingMaximum is { } max ? max + 1 : 1);
        return start + rowIndex;
    }
}

public class RandomIntGenerator : IDataGenerator
{
    public string Name => "Random Integer";
    public string Description => "Random whole number within a range";

    public bool Supports(ColumnTypeShape type) =>
        type.Family is ColumnTypeFamily.Integer or ColumnTypeFamily.Decimal or ColumnTypeFamily.Float;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var (min, max) = Range(options);
        return Random.Shared.NextInt64(min, max + 1);
    }

    internal static (long Min, long Max) Range(DataGeneratorOptions options)
    {
        long min = options.MinValue ?? 0;
        long max = options.MaxValue ?? 1000;
        return min <= max ? (min, max) : (max, min);
    }
}

public class RandomNumberGenerator : IDataGenerator
{
    public string Name => "Random Decimal";
    public string Description => "Random number with two decimal places within a range";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Decimal or ColumnTypeFamily.Float;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var (min, max) = RandomIntGenerator.Range(options);
        var value = min + (decimal)Random.Shared.NextDouble() * (max - min);
        return Math.Clamp(decimal.Round(value, 2), min, max);
    }
}

public class RandomTextGenerator : IDataGenerator
{
    private const string Chars = "abcdefghijklmnopqrstuvwxyz";

    public string Name => "Random Text";
    public string Description => "Random alphabetic string";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var min = Math.Max(0, options.MinLength ?? 5);
        var max = Math.Max(min, options.MaxLength ?? 20);
        var length = Random.Shared.Next(min, max + 1);
        return new string(Enumerable.Range(0, length).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray());
    }
}

public class NameGenerator : IDataGenerator
{
    private static readonly string[] FirstNames =
    [
        "Alice", "Bob", "Charlie", "Diana", "Edward", "Fiona", "George", "Hannah",
        "Ivan", "Julia", "Kevin", "Laura", "Michael", "Nina", "Oscar", "Patricia",
        "Quinn", "Rachel", "Samuel", "Tara", "Uma", "Victor", "Wendy", "Xavier"
    ];

    private static readonly string[] LastNames =
    [
        "Anderson", "Brown", "Clark", "Davis", "Evans", "Foster", "Garcia", "Harris",
        "Ito", "Johnson", "Kim", "Lee", "Martinez", "Nelson", "O'Brien", "Patel",
        "Quinn", "Roberts", "Smith", "Thompson", "Underwood", "Vargas", "Wilson", "Zhang"
    ];

    public string Name => "Name";
    public string Description => "Random full name (first + last)";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => $"{FirstNames[Random.Shared.Next(FirstNames.Length)]} {LastNames[Random.Shared.Next(LastNames.Length)]}";
}

public class EmailGenerator : IDataGenerator
{
    private static readonly string[] Domains = ["example.com", "test.org", "sample.net", "demo.io"];
    private const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string Name => "Email";
    public string Description => "Random email address";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var local = new string(Enumerable.Range(0, Random.Shared.Next(5, 12)).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray());
        return $"{local}@{Domains[Random.Shared.Next(Domains.Length)]}";
    }
}

/// <summary>
/// Makes a moment in the chosen date range, to whole seconds so every engine's date and time types accept it.
/// The service keeps only the part a column holds: the date for a date, the time of day for a time.
/// </summary>
public class DateRangeGenerator : IDataGenerator
{
    private const int SecondsPerDay = 24 * 60 * 60;

    public string Name => "Date Range";
    public string Description => "Random date and time within a range";

    // SQLite has no date type, so dates live in text columns there.
    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Date or ColumnTypeFamily.DateTime
        or ColumnTypeFamily.DateTimeOffset or ColumnTypeFamily.Time or ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var min = (options.MinDate ?? new DateTime(2020, 1, 1)).Date;
        var max = (options.MaxDate ?? DateTime.Today).Date;
        if (max < min)
            (min, max) = (max, min);

        var days = (int)(max - min).TotalDays;
        return min.AddDays(Random.Shared.Next(days + 1)).AddSeconds(Random.Shared.Next(SecondsPerDay));
    }
}

public class UuidGenerator : IDataGenerator
{
    public string Name => "UUID";
    public string Description => "Random UUID/GUID";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Uuid or ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options) => Guid.NewGuid();
}

public class BooleanGenerator : IDataGenerator
{
    public string Name => "Boolean";
    public string Description => "Random true/false value";

    // Integer columns are how SQLite and older schemas store flags; the value is written as 1 or 0 there.
    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Boolean or ColumnTypeFamily.Integer;

    public object? Generate(int rowIndex, DataGeneratorOptions options) => Random.Shared.Next(2) == 1;
}

public class JsonGenerator : IDataGenerator
{
    public string Name => "JSON Object";
    public string Description => "Random JSON object with key-value pairs";

    public bool Supports(ColumnTypeShape type) => type.Family is ColumnTypeFamily.Json or ColumnTypeFamily.Text;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => string.Create(CultureInfo.InvariantCulture, $"{{\"id\": {rowIndex + 1}, \"value\": {Random.Shared.Next(1000)}}}");
}

/// <summary>
/// Fills a foreign key with values the referenced column already holds, so every row satisfies the constraint.
/// </summary>
public class ReferencedValueGenerator : IDataGenerator
{
    public string Name => "Existing Reference";
    public string Description => "A value that already exists in the referenced column";

    public bool Supports(ColumnTypeShape type) => type.Family is not ColumnTypeFamily.RowVersion;

    public object? Generate(int rowIndex, DataGeneratorOptions options) =>
        options.ReferencedValues is { Count: > 0 } values ? values[Random.Shared.Next(values.Count)] : null;
}

public class CustomListGenerator : IDataGenerator
{
    public string Name => "Custom List";
    public string Description => "Random value from a comma-separated list";

    public bool Supports(ColumnTypeShape type) => type.Family is not ColumnTypeFamily.RowVersion;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var values = Values(options);
        return values.Length == 0 ? null : values[Random.Shared.Next(values.Length)];
    }

    public static string[] Values(DataGeneratorOptions options) =>
        string.IsNullOrWhiteSpace(options.CustomValues)
            ? []
            : options.CustomValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Leaves the column out of the INSERT so the database fills it in: with its default, a computed value, or NULL.
/// </summary>
public class DatabaseDefaultGenerator : IDataGenerator
{
    public string Name => "Leave Out";
    public string Description => "Let the database fill the column: its default, a computed value, or NULL";

    public bool Supports(ColumnTypeShape type) => true;

    public object? Generate(int rowIndex, DataGeneratorOptions options) => null;
}

public class NullGenerator : IDataGenerator
{
    public string Name => "NULL";
    public string Description => "Always generates NULL";
    public bool Supports(ColumnTypeShape type) => true;
    public object? Generate(int rowIndex, DataGeneratorOptions options) => null;
}

public static class DataGenerators
{
    public static IReadOnlyList<IDataGenerator> All { get; } =
    [
        new RandomIntGenerator(),
        new RandomNumberGenerator(),
        new AutoIncrementGenerator(),
        new RandomTextGenerator(),
        new NameGenerator(),
        new EmailGenerator(),
        new DateRangeGenerator(),
        new UuidGenerator(),
        new BooleanGenerator(),
        new JsonGenerator(),
        new ReferencedValueGenerator(),
        new CustomListGenerator(),
        new DatabaseDefaultGenerator(),
        new NullGenerator()
    ];

    public static T Get<T>() where T : IDataGenerator => All.OfType<T>().First();
}
