namespace Aion.Components.Scaffolding.DataGeneration;

public class AutoIncrementGenerator : IDataGenerator
{
    public string Name => "Auto Increment";
    public string Description => "Sequential integer starting from a given value";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "integer" or "int" or "bigint" or "smallint" or "serial" or "bigserial"
            or "numeric" or "decimal" or "real" or "double precision"
            or "INTEGER" or "NUMERIC" or "REAL";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => (options.StartValue ?? 1) + rowIndex;
}

public class RandomIntGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

    public string Name => "Random Integer";
    public string Description => "Random integer within a range";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "integer" or "int" or "bigint" or "smallint" or "serial" or "bigserial"
            or "numeric" or "decimal" or "real" or "double precision"
            or "INTEGER" or "NUMERIC" or "REAL";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => Rng.Next(options.MinValue ?? 0, (options.MaxValue ?? 1000) + 1);
}

public class RandomTextGenerator : IDataGenerator
{
    private static readonly Random Rng = new();
    private const string Chars = "abcdefghijklmnopqrstuvwxyz";

    public string Name => "Random Text";
    public string Description => "Random alphabetic string";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "text" or "varchar" or "char" or "character varying"
            or "TEXT";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var length = Rng.Next(options.MinLength ?? 5, (options.MaxLength ?? 20) + 1);
        return new string(Enumerable.Range(0, length).Select(_ => Chars[Rng.Next(Chars.Length)]).ToArray());
    }
}

public class NameGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

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

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "text" or "varchar" or "char" or "character varying"
            or "TEXT";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => $"{FirstNames[Rng.Next(FirstNames.Length)]} {LastNames[Rng.Next(LastNames.Length)]}";
}

public class EmailGenerator : IDataGenerator
{
    private static readonly Random Rng = new();
    private static readonly string[] Domains = ["example.com", "test.org", "sample.net", "demo.io"];
    private const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string Name => "Email";
    public string Description => "Random email address";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "text" or "varchar" or "char" or "character varying"
            or "TEXT";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var local = new string(Enumerable.Range(0, Rng.Next(5, 12)).Select(_ => Chars[Rng.Next(Chars.Length)]).ToArray());
        return $"{local}@{Domains[Rng.Next(Domains.Length)]}";
    }
}

public class DateRangeGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

    public string Name => "Date Range";
    public string Description => "Random date within a range";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "date" or "timestamp" or "timestamptz" or "time" or "interval"
            or "datetime" or "datetime2" or "datetimeoffset";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        var min = options.MinDate ?? new DateTime(2020, 1, 1);
        var max = options.MaxDate ?? DateTime.Now;
        var range = (max - min).Days;
        if (range <= 0) range = 1;
        return min.AddDays(Rng.Next(range)).ToString("yyyy-MM-dd");
    }
}

public class UuidGenerator : IDataGenerator
{
    public string Name => "UUID";
    public string Description => "Random UUID/GUID";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "uuid" or "uniqueidentifier" or "text" or "varchar"
            or "TEXT";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => Guid.NewGuid().ToString();
}

public class BooleanGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

    public string Name => "Boolean";
    public string Description => "Random true/false value";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "boolean" or "bool" or "bit"
            or "INTEGER"; // SQLite uses integer for booleans
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => Rng.Next(2) == 1;
}

public class JsonGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

    public string Name => "JSON Object";
    public string Description => "Random JSON object with key-value pairs";

    public bool SupportsType(string dataType)
    {
        var lower = dataType.ToLowerInvariant();
        return lower is "json" or "jsonb" or "text" or "TEXT";
    }

    public object? Generate(int rowIndex, DataGeneratorOptions options)
        => $"{{\"id\": {rowIndex + 1}, \"value\": {Rng.Next(1000)}}}";
}

public class CustomListGenerator : IDataGenerator
{
    private static readonly Random Rng = new();

    public string Name => "Custom List";
    public string Description => "Random value from a comma-separated list";

    public bool SupportsType(string dataType) => true;

    public object? Generate(int rowIndex, DataGeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CustomValues))
            return null;

        var values = options.CustomValues
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (values.Length == 0)
            return null;

        return values[Rng.Next(values.Length)];
    }
}

public class NullGenerator : IDataGenerator
{
    public string Name => "NULL";
    public string Description => "Always generates NULL";
    public bool SupportsType(string dataType) => true;
    public object? Generate(int rowIndex, DataGeneratorOptions options) => null;
}

public static class DataGenerators
{
    public static IReadOnlyList<IDataGenerator> All { get; } =
    [
        new AutoIncrementGenerator(),
        new RandomIntGenerator(),
        new RandomTextGenerator(),
        new NameGenerator(),
        new EmailGenerator(),
        new DateRangeGenerator(),
        new UuidGenerator(),
        new BooleanGenerator(),
        new JsonGenerator(),
        new CustomListGenerator(),
        new NullGenerator()
    ];

    public static IDataGenerator? SuggestGenerator(string columnName, string dataType, bool isIdentity)
    {
        if (isIdentity)
            return null;

        var lower = columnName.ToLowerInvariant();

        if (lower.Contains("email") || lower.Contains("e_mail"))
            return All.First(g => g is EmailGenerator);

        if (lower.Contains("name") || lower == "first_name" || lower == "last_name" || lower == "full_name")
            return All.First(g => g is NameGenerator);

        if (lower.Contains("uuid") || lower.Contains("guid"))
            return All.First(g => g is UuidGenerator);

        if (lower.Contains("date") || lower.Contains("created") || lower.Contains("updated"))
        {
            var dateGen = All.First(g => g is DateRangeGenerator);
            if (dateGen.SupportsType(dataType))
                return dateGen;
        }

        if (lower.Contains("active") || lower.Contains("enabled") || lower.Contains("is_"))
        {
            var boolGen = All.First(g => g is BooleanGenerator);
            if (boolGen.SupportsType(dataType))
                return boolGen;
        }

        var compatible = All.Where(g => g is not NullGenerator and not CustomListGenerator && g.SupportsType(dataType)).ToList();
        return compatible.FirstOrDefault();
    }
}
