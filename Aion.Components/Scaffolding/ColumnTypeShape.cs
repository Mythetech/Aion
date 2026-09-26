using System.Globalization;
using System.Text.RegularExpressions;
using Aion.Components.Connections;
using Aion.Contracts.Database;

namespace Aion.Components.Scaffolding;

/// <summary>
/// What kind of value a column holds, whatever the engine calls its type.
/// </summary>
public enum ColumnTypeFamily
{
    Unknown,
    Integer,
    Decimal,
    Float,
    Boolean,
    Text,
    Date,
    DateTime,
    DateTimeOffset,
    Time,
    Interval,
    Uuid,
    Json,
    Binary,

    /// <summary>SQL Server's rowversion, which the engine writes itself and never accepts a value for.</summary>
    RowVersion
}

/// <summary>
/// A column's type reduced to what the schema wizard and data generation act on: the family of values it holds
/// and the limits a value must fit. Engines and catalogs spell the same type many ways ("int4", "integer",
/// "character varying(255)", "varchar", "timestamptz", "timestamp with time zone", "tinyint(1)"), so every
/// decision about a type goes through here instead of comparing names.
/// </summary>
public sealed partial record ColumnTypeShape(string Name, ColumnTypeFamily Family, int? MaxLength = null, long? MinInteger = null, long? MaxInteger = null)
{
    private static readonly Dictionary<string, ColumnTypeFamily> Families = new(StringComparer.Ordinal)
    {
        ["int"] = ColumnTypeFamily.Integer,
        ["integer"] = ColumnTypeFamily.Integer,
        ["int2"] = ColumnTypeFamily.Integer,
        ["int4"] = ColumnTypeFamily.Integer,
        ["int8"] = ColumnTypeFamily.Integer,
        ["tinyint"] = ColumnTypeFamily.Integer,
        ["smallint"] = ColumnTypeFamily.Integer,
        ["mediumint"] = ColumnTypeFamily.Integer,
        ["bigint"] = ColumnTypeFamily.Integer,
        ["serial"] = ColumnTypeFamily.Integer,
        ["serial2"] = ColumnTypeFamily.Integer,
        ["serial4"] = ColumnTypeFamily.Integer,
        ["serial8"] = ColumnTypeFamily.Integer,
        ["smallserial"] = ColumnTypeFamily.Integer,
        ["bigserial"] = ColumnTypeFamily.Integer,
        ["year"] = ColumnTypeFamily.Integer,

        ["bool"] = ColumnTypeFamily.Boolean,
        ["boolean"] = ColumnTypeFamily.Boolean,

        ["numeric"] = ColumnTypeFamily.Decimal,
        ["decimal"] = ColumnTypeFamily.Decimal,
        ["dec"] = ColumnTypeFamily.Decimal,
        ["fixed"] = ColumnTypeFamily.Decimal,
        ["number"] = ColumnTypeFamily.Decimal,
        ["money"] = ColumnTypeFamily.Decimal,
        ["smallmoney"] = ColumnTypeFamily.Decimal,

        ["real"] = ColumnTypeFamily.Float,
        ["float"] = ColumnTypeFamily.Float,
        ["float4"] = ColumnTypeFamily.Float,
        ["float8"] = ColumnTypeFamily.Float,
        ["double"] = ColumnTypeFamily.Float,

        ["text"] = ColumnTypeFamily.Text,
        ["varchar"] = ColumnTypeFamily.Text,
        ["char"] = ColumnTypeFamily.Text,
        ["bpchar"] = ColumnTypeFamily.Text,
        ["nvarchar"] = ColumnTypeFamily.Text,
        ["nchar"] = ColumnTypeFamily.Text,
        ["ntext"] = ColumnTypeFamily.Text,
        ["tinytext"] = ColumnTypeFamily.Text,
        ["mediumtext"] = ColumnTypeFamily.Text,
        ["longtext"] = ColumnTypeFamily.Text,
        ["citext"] = ColumnTypeFamily.Text,
        ["clob"] = ColumnTypeFamily.Text,
        ["string"] = ColumnTypeFamily.Text,
        ["sysname"] = ColumnTypeFamily.Text,
        ["varchar2"] = ColumnTypeFamily.Text,
        ["nvarchar2"] = ColumnTypeFamily.Text,
        ["native character"] = ColumnTypeFamily.Text,
        ["varying character"] = ColumnTypeFamily.Text,

        ["date"] = ColumnTypeFamily.Date,
        ["datetime"] = ColumnTypeFamily.DateTime,
        ["datetime2"] = ColumnTypeFamily.DateTime,
        ["smalldatetime"] = ColumnTypeFamily.DateTime,
        ["timestamp"] = ColumnTypeFamily.DateTime,
        ["timestamptz"] = ColumnTypeFamily.DateTimeOffset,
        ["datetimeoffset"] = ColumnTypeFamily.DateTimeOffset,
        ["time"] = ColumnTypeFamily.Time,
        ["timetz"] = ColumnTypeFamily.Time,
        ["interval"] = ColumnTypeFamily.Interval,

        ["uuid"] = ColumnTypeFamily.Uuid,
        ["uniqueidentifier"] = ColumnTypeFamily.Uuid,

        ["json"] = ColumnTypeFamily.Json,
        ["jsonb"] = ColumnTypeFamily.Json,

        ["bytea"] = ColumnTypeFamily.Binary,
        ["blob"] = ColumnTypeFamily.Binary,
        ["tinyblob"] = ColumnTypeFamily.Binary,
        ["mediumblob"] = ColumnTypeFamily.Binary,
        ["longblob"] = ColumnTypeFamily.Binary,
        ["binary"] = ColumnTypeFamily.Binary,
        ["varbinary"] = ColumnTypeFamily.Binary,
        ["image"] = ColumnTypeFamily.Binary,

        ["rowversion"] = ColumnTypeFamily.RowVersion
    };

    // MySQL's tinyint is signed and SQL Server's is unsigned, so only the range both accept is used.
    private static readonly Dictionary<string, (long Min, long Max)> IntegerRanges = new(StringComparer.Ordinal)
    {
        ["tinyint"] = (0, 127),
        ["smallint"] = (short.MinValue, short.MaxValue),
        ["int2"] = (short.MinValue, short.MaxValue),
        ["smallserial"] = (1, short.MaxValue),
        ["serial2"] = (1, short.MaxValue),
        ["mediumint"] = (-8_388_608, 8_388_607),
        ["int"] = (int.MinValue, int.MaxValue),
        ["integer"] = (int.MinValue, int.MaxValue),
        ["int4"] = (int.MinValue, int.MaxValue),
        ["serial"] = (1, int.MaxValue),
        ["serial4"] = (1, int.MaxValue),
        ["bigint"] = (long.MinValue, long.MaxValue),
        ["int8"] = (long.MinValue, long.MaxValue),
        ["bigserial"] = (1, long.MaxValue),
        ["serial8"] = (1, long.MaxValue),
        ["year"] = (1901, 2155)
    };

    private static readonly string[] MySqlModifiers = [" unsigned", " signed", " zerofill"];

    public static ColumnTypeShape Of(ColumnInfo column, DatabaseType engine) => Of(column.DataType, engine, column.MaxLength);

    /// <param name="catalogLength">The length the catalog reports beside the type, as MySQL, SQL Server and
    /// PostgreSQL's information_schema do. SQL Server reports -1 for (max).</param>
    public static ColumnTypeShape Of(string? dataType, DatabaseType engine, int? catalogLength = null)
    {
        var (name, arguments, suffix) = Split(ColumnTypeText.Short(dataType, engine));
        if (name.Length == 0 || name.EndsWith("[]", StringComparison.Ordinal))
            return new ColumnTypeShape(name, ColumnTypeFamily.Unknown);

        // "timestamp(3) with time zone" keeps its zone after the precision, where the short-name lookup can't see it.
        if (suffix.Contains("with time zone", StringComparison.Ordinal))
            name = name == "time" ? "timetz" : "timestamptz";

        var family = FamilyOf(name, arguments, engine);
        return family switch
        {
            ColumnTypeFamily.Text => new ColumnTypeShape(name, family, MaxLength: Length(arguments, catalogLength)),
            ColumnTypeFamily.Integer => WithRange(new ColumnTypeShape(name, family), IntegerRange(name, engine)),
            ColumnTypeFamily.Decimal => WithRange(new ColumnTypeShape(name, family), DecimalRange(arguments)),
            _ => new ColumnTypeShape(name, family)
        };
    }

    private static ColumnTypeFamily FamilyOf(string name, string? arguments, DatabaseType engine)
    {
        switch (name)
        {
            // MySQL spells BOOLEAN as tinyint(1); a wider tinyint is a small number.
            case "tinyint" when engine == DatabaseType.MySQL && arguments == "1":
                return ColumnTypeFamily.Boolean;
            case "bit" when engine == DatabaseType.SQLServer:
            case "bit" when engine == DatabaseType.MySQL && arguments is null or "1":
                return ColumnTypeFamily.Boolean;
        }

        if (Families.TryGetValue(name, out var family))
            return family;

        return IsSqlite(engine) ? SqliteAffinity(name) : ColumnTypeFamily.Unknown;
    }

    // SQLite accepts any type name and decides how to store values from the words in it.
    // https://www.sqlite.org/datatype3.html#determination_of_column_affinity
    private static ColumnTypeFamily SqliteAffinity(string name)
    {
        if (name.Contains("int", StringComparison.Ordinal))
            return ColumnTypeFamily.Integer;
        if (name.Contains("char", StringComparison.Ordinal) || name.Contains("clob", StringComparison.Ordinal) || name.Contains("text", StringComparison.Ordinal))
            return ColumnTypeFamily.Text;
        if (name.Contains("blob", StringComparison.Ordinal))
            return ColumnTypeFamily.Binary;
        if (name.Contains("real", StringComparison.Ordinal) || name.Contains("floa", StringComparison.Ordinal) || name.Contains("doub", StringComparison.Ordinal))
            return ColumnTypeFamily.Float;
        return ColumnTypeFamily.Decimal;
    }

    private static bool IsSqlite(DatabaseType engine) => engine == DatabaseType.WasmSQLite;

    private static (long Min, long Max)? IntegerRange(string name, DatabaseType engine)
    {
        // Every SQLite integer is stored in up to eight bytes, whatever the declared name.
        if (IsSqlite(engine))
            return (long.MinValue, long.MaxValue);

        return IntegerRanges.TryGetValue(name, out var range) ? range : null;
    }

    private static (long Min, long Max)? DecimalRange(string? arguments)
    {
        if (arguments is null)
            return null;

        var parts = arguments.Split(',');
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var precision))
            return null;

        var scale = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
        var wholeDigits = precision - scale;
        if (wholeDigits is < 0 or > 18)
            return null;

        var max = (long)Math.Pow(10, wholeDigits) - 1;
        return (-max, max);
    }

    private static int? Length(string? arguments, int? catalogLength)
    {
        if (arguments is not null)
            return int.TryParse(arguments, NumberStyles.None, CultureInfo.InvariantCulture, out var declared) && declared > 0 ? declared : null;

        return catalogLength > 0 ? catalogLength : null;
    }

    private static ColumnTypeShape WithRange(ColumnTypeShape shape, (long Min, long Max)? range) =>
        range is { } r ? shape with { MinInteger = r.Min, MaxInteger = r.Max } : shape;

    private static (string Name, string? Arguments, string Suffix) Split(string type)
    {
        var match = TypeParts().Match(type);
        if (!match.Success)
            return (type, null, "");

        var name = StripModifiers(match.Groups["name"].Value.Trim());
        var arguments = match.Groups["args"].Success ? match.Groups["args"].Value.Trim() : null;
        return (name, arguments, match.Groups["suffix"].Value.Trim());
    }

    private static string StripModifiers(string name)
    {
        foreach (var modifier in MySqlModifiers)
        {
            if (name.EndsWith(modifier, StringComparison.Ordinal))
                return StripModifiers(name[..^modifier.Length]);
        }

        return name;
    }

    [GeneratedRegex(@"^(?<name>[^(]*)(\((?<args>[^)]*)\))?(?<suffix>.*)$", RegexOptions.Singleline)]
    private static partial Regex TypeParts();
}
