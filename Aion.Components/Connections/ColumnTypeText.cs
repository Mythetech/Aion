using System.Text.RegularExpressions;
using Aion.Contracts.Database;

namespace Aion.Components.Connections;

/// <summary>
/// Formats column metadata for the schema tree and the results grid: a short type that fits beside the
/// column name and a full description for the hover tooltip. PostgreSQL and PGlite report information_schema spellings
/// ("character varying", "timestamp with time zone"), MySQL and SQL Server report short lowercase
/// names with the length in a separate field, and SQLite reports the declared type text as written.
/// </summary>
public static partial class ColumnTypeText
{
    private const string Separator = " · ";

    private static readonly Dictionary<string, string> ShortNames = new(StringComparer.Ordinal)
    {
        ["character varying"] = "varchar",
        ["character"] = "char",
        ["bit varying"] = "varbit",
        ["double precision"] = "double",
        ["timestamp without time zone"] = "timestamp",
        ["timestamp with time zone"] = "timestamptz",
        ["time without time zone"] = "time",
        ["time with time zone"] = "timetz"
    };

    // PostgreSQL's internal type names, as udt_name reports them, spelled the way information_schema spells the
    // same type on a plain column, so an integer[] column reads like an integer one.
    private static readonly Dictionary<string, string> PostgresInternalNames = new(StringComparer.Ordinal)
    {
        ["int2"] = "smallint",
        ["int4"] = "integer",
        ["int8"] = "bigint",
        ["float4"] = "real",
        ["float8"] = "double precision",
        ["bool"] = "boolean",
        ["bpchar"] = "character",
        ["varchar"] = "character varying",
        ["varbit"] = "bit varying",
        ["timestamp"] = "timestamp without time zone",
        ["timestamptz"] = "timestamp with time zone",
        ["time"] = "time without time zone",
        ["timetz"] = "time with time zone"
    };

    // MySQL and SQL Server also report a length for text, blob, enum, xml and spatial columns, but that is a
    // storage limit or value width rather than something written in the type, so it is left off.
    private static readonly HashSet<string> SizedTypes = new(StringComparer.Ordinal)
    {
        "char", "varchar", "nchar", "nvarchar", "binary", "varbinary", "bit", "varbit",
        "character", "character varying", "bit varying"
    };

    public static string Short(ColumnInfo column, DatabaseType engine)
    {
        if (PostgresTypeName(column, engine) is { } named)
            return named.EndsWith("[]", StringComparison.Ordinal) ? ShortName(named[..^2], engine) + "[]" : named;

        var type = Tidy(column.DataType).ToLowerInvariant();
        if (type.Length == 0)
            return "";

        var (name, arguments) = SplitArguments(type);
        name = ShortName(name, engine);

        return name + (arguments ?? Length(column, name, engine));
    }

    /// <summary>
    /// The short form of a type as a query result reports it, where any length is already part of the name.
    /// </summary>
    public static string Short(string? dataType, DatabaseType engine)
    {
        var type = Tidy(dataType).ToLowerInvariant();
        if (type.Length == 0)
            return "";

        var (name, arguments) = SplitArguments(type);
        return name.EndsWith("[]", StringComparison.Ordinal)
            ? ShortName(name[..^2], engine) + "[]" + arguments
            : ShortName(name, engine) + arguments;
    }

    public static string Describe(ColumnInfo column, DatabaseType engine)
    {
        var type = Tidy(column.DataType);
        var named = PostgresTypeName(column, engine);
        var parts = new List<string>
        {
            named is not null ? (IsUserDefined(column) ? $"{named} (user-defined type)" : named)
            : type.Length == 0 ? "no declared type"
            : type + (type.Contains('(') ? "" : Length(column, type.ToLowerInvariant(), engine)),
            column.IsNullable ? "NULL" : "NOT NULL"
        };

        if (column.IsPrimaryKey)
            parts.Add("primary key");

        if (column.IsIdentity)
            parts.Add("identity");

        if (column.ForeignKey is { } foreignKey)
            parts.Add($"references {ReferencedTable(foreignKey)}({foreignKey.ReferencedColumn})");

        if (!string.IsNullOrWhiteSpace(column.DefaultValue))
            parts.Add($"default {column.DefaultValue}");

        return string.Join(Separator, parts);
    }

    // information_schema names only the category of an array or a user-defined type (enum, composite, or an
    // extension type such as citext); udt_name holds the type itself, with a leading underscore on an array type.
    private static string? PostgresTypeName(ColumnInfo column, DatabaseType engine)
    {
        if (engine is not (DatabaseType.PostgreSQL or DatabaseType.WasmPostgreSQL) || string.IsNullOrEmpty(column.UdtName))
            return null;

        if (column.DataType.Equals("ARRAY", StringComparison.OrdinalIgnoreCase) && column.UdtName.StartsWith('_'))
        {
            var element = column.UdtName[1..];
            return PostgresInternalNames.GetValueOrDefault(element, element) + "[]";
        }

        return IsUserDefined(column) ? column.UdtName : null;
    }

    private static bool IsUserDefined(ColumnInfo column) =>
        column.DataType.Equals("USER-DEFINED", StringComparison.OrdinalIgnoreCase);

    private static string ShortName(string name, DatabaseType engine)
    {
        // SQL Server reports rowversion columns under the deprecated synonym "timestamp", which reads like a date.
        if (engine == DatabaseType.SQLServer && name == "timestamp")
            return "rowversion";

        return ShortNames.GetValueOrDefault(name, name);
    }

    private static string Length(ColumnInfo column, string typeName, DatabaseType engine)
    {
        if (column.MaxLength is not { } length || !SizedTypes.Contains(typeName))
            return "";

        if (length == -1 && engine == DatabaseType.SQLServer)
            return "(max)";

        return length > 0 ? $"({length})" : "";
    }

    private static string ReferencedTable(ForeignKeyInfo foreignKey) =>
        string.IsNullOrEmpty(foreignKey.ReferencedSchema)
            ? foreignKey.ReferencedTable
            : $"{foreignKey.ReferencedSchema}.{foreignKey.ReferencedTable}";

    private static (string Name, string? Arguments) SplitArguments(string type)
    {
        var open = type.IndexOf('(');
        return open < 0 ? (type, null) : (type[..open].TrimEnd(), type[open..]);
    }

    // SQLite keeps the declared type exactly as typed, so "VARCHAR( 255 )" and "DECIMAL(10, 2)" arrive with their spacing.
    private static string Tidy(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "";

        var collapsed = Whitespace().Replace(type.Trim(), " ");
        var tightOpenings = SpaceAroundOpeningOrComma().Replace(collapsed, "$1");
        return SpaceBeforeClosing().Replace(tightOpenings, ")");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\s*([(,])\s*")]
    private static partial Regex SpaceAroundOpeningOrComma();

    [GeneratedRegex(@"\s+\)")]
    private static partial Regex SpaceBeforeClosing();
}
