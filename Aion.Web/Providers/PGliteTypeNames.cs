namespace Aion.Web.Providers;

/// <summary>
/// Names PGlite result columns from the type ids its fields report, using the spellings information_schema
/// uses so the grid shortens them the same way as the schema tree. Ids of user types, such as enums and
/// domains, differ per database and are left unnamed.
/// </summary>
public static class PGliteTypeNames
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [16] = "boolean",
        [17] = "bytea",
        [18] = "char",
        [19] = "name",
        [20] = "bigint",
        [21] = "smallint",
        [23] = "integer",
        [25] = "text",
        [26] = "oid",
        [114] = "json",
        [142] = "xml",
        [650] = "cidr",
        [700] = "real",
        [701] = "double precision",
        [790] = "money",
        [829] = "macaddr",
        [869] = "inet",
        [1000] = "boolean[]",
        [1005] = "smallint[]",
        [1007] = "integer[]",
        [1009] = "text[]",
        [1015] = "character varying[]",
        [1016] = "bigint[]",
        [1021] = "real[]",
        [1022] = "double precision[]",
        [1042] = "character",
        [1043] = "character varying",
        [1082] = "date",
        [1083] = "time without time zone",
        [1114] = "timestamp without time zone",
        [1184] = "timestamp with time zone",
        [1186] = "interval",
        [1231] = "numeric[]",
        [1266] = "time with time zone",
        [1560] = "bit",
        [1562] = "bit varying",
        [1700] = "numeric",
        [2950] = "uuid",
        [3614] = "tsvector",
        [3802] = "jsonb"
    };

    public static string? For(int typeId) => Names.GetValueOrDefault(typeId);
}
