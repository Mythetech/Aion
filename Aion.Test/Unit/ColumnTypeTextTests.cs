using Aion.Components.Connections;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit;

/// <summary>
/// The inputs are the type names and lengths each provider actually reports, captured from PostgreSQL,
/// MySQL 8.0 and SQL Server 2022 information_schema queries and from SQLite's PRAGMA table_info.
/// </summary>
public class ColumnTypeTextTests
{
    private static ColumnInfo Column(string dataType, int? maxLength = null) => new()
    {
        Name = "c",
        DataType = dataType,
        MaxLength = maxLength,
        IsNullable = true
    };

    [Theory]
    [InlineData("integer", null, "integer")]
    [InlineData("bigint", null, "bigint")]
    [InlineData("character varying", 255, "varchar(255)")]
    [InlineData("character varying", null, "varchar")]
    [InlineData("character", 3, "char(3)")]
    [InlineData("text", null, "text")]
    [InlineData("timestamp without time zone", null, "timestamp")]
    [InlineData("timestamp with time zone", null, "timestamptz")]
    [InlineData("time without time zone", null, "time")]
    [InlineData("time with time zone", null, "timetz")]
    [InlineData("double precision", null, "double")]
    [InlineData("numeric", null, "numeric")]
    [InlineData("bit", 8, "bit(8)")]
    [InlineData("bit varying", 8, "varbit(8)")]
    [InlineData("jsonb", null, "jsonb")]
    [InlineData("ARRAY", null, "array")]
    [InlineData("USER-DEFINED", null, "user-defined")]
    public void Short_PostgreSql_UsesCompactNames(string dataType, int? maxLength, string expected)
    {
        ColumnTypeText.Short(Column(dataType, maxLength), DatabaseType.PostgreSQL).ShouldBe(expected);
        ColumnTypeText.Short(Column(dataType, maxLength), DatabaseType.WasmPostgreSQL).ShouldBe(expected);
    }

    // information_schema names only the category of an array or a user-defined type; udt_name has the type itself,
    // with a leading underscore for the array type of an element.
    [Theory]
    [InlineData("ARRAY", "_int4", "integer[]")]
    [InlineData("ARRAY", "_int8", "bigint[]")]
    [InlineData("ARRAY", "_float8", "double[]")]
    [InlineData("ARRAY", "_bool", "boolean[]")]
    [InlineData("ARRAY", "_varchar", "varchar[]")]
    [InlineData("ARRAY", "_bpchar", "char[]")]
    [InlineData("ARRAY", "_timestamptz", "timestamptz[]")]
    [InlineData("ARRAY", "_text", "text[]")]
    [InlineData("ARRAY", "_mood", "mood[]")]
    [InlineData("USER-DEFINED", "mood", "mood")]
    [InlineData("USER-DEFINED", "citext", "citext")]
    [InlineData("ARRAY", null, "array")]
    [InlineData("USER-DEFINED", "", "user-defined")]
    public void Short_PostgreSql_NamesArraysAndUserDefinedTypesFromTheirUdtName(string dataType, string? udtName, string expected)
    {
        var column = Column(dataType);
        column.UdtName = udtName;

        ColumnTypeText.Short(column, DatabaseType.PostgreSQL).ShouldBe(expected);
        ColumnTypeText.Short(column, DatabaseType.WasmPostgreSQL).ShouldBe(expected);
    }

    [Theory]
    [InlineData("ARRAY", "_int4", "integer[] · NULL")]
    [InlineData("ARRAY", "_varchar", "character varying[] · NULL")]
    [InlineData("ARRAY", "_timestamptz", "timestamp with time zone[] · NULL")]
    [InlineData("USER-DEFINED", "mood", "mood (user-defined type) · NULL")]
    public void Describe_PostgreSql_SpellsOutArrayAndUserDefinedTypes(string dataType, string udtName, string expected)
    {
        var column = Column(dataType);
        column.UdtName = udtName;

        ColumnTypeText.Describe(column, DatabaseType.PostgreSQL).ShouldBe(expected);
    }

    [Fact]
    public void Short_UdtName_IsOnlyReadForPostgreSqlCategories()
    {
        var column = Column("int");
        column.UdtName = "_int4";

        ColumnTypeText.Short(column, DatabaseType.MySQL).ShouldBe("int");
        ColumnTypeText.Short(column, DatabaseType.PostgreSQL).ShouldBe("int");
    }

    [Theory]
    [InlineData("int", null, "int")]
    [InlineData("varchar", 255, "varchar(255)")]
    [InlineData("char", 3, "char(3)")]
    [InlineData("varbinary", 255, "varbinary(255)")]
    [InlineData("text", 65535, "text")]
    [InlineData("tinytext", 255, "tinytext")]
    [InlineData("longtext", 2147483647, "longtext")]
    [InlineData("blob", 65535, "blob")]
    [InlineData("enum", 2, "enum")]
    [InlineData("set", 3, "set")]
    [InlineData("decimal", null, "decimal")]
    [InlineData("datetime", null, "datetime")]
    public void Short_MySql_ShowsLengthOnlyForSizedStringsAndBinaries(string dataType, int? maxLength, string expected)
    {
        ColumnTypeText.Short(Column(dataType, maxLength), DatabaseType.MySQL).ShouldBe(expected);
    }

    [Theory]
    [InlineData("int", null, "int")]
    [InlineData("nvarchar", 100, "nvarchar(100)")]
    [InlineData("nvarchar", -1, "nvarchar(max)")]
    [InlineData("varchar", -1, "varchar(max)")]
    [InlineData("varbinary", -1, "varbinary(max)")]
    [InlineData("nchar", 3, "nchar(3)")]
    [InlineData("text", 2147483647, "text")]
    [InlineData("ntext", 1073741823, "ntext")]
    [InlineData("image", 2147483647, "image")]
    [InlineData("xml", -1, "xml")]
    [InlineData("geography", -1, "geography")]
    [InlineData("hierarchyid", 892, "hierarchyid")]
    [InlineData("sql_variant", 0, "sql_variant")]
    [InlineData("datetime2", null, "datetime2")]
    [InlineData("uniqueidentifier", null, "uniqueidentifier")]
    public void Short_SqlServer_ShowsMaxForUnboundedLengths(string dataType, int? maxLength, string expected)
    {
        ColumnTypeText.Short(Column(dataType, maxLength), DatabaseType.SQLServer).ShouldBe(expected);
    }

    [Fact]
    public void Short_SqlServer_NamesTimestampAsRowversion()
    {
        // SQL Server reports rowversion columns under their deprecated synonym, which reads like a date.
        ColumnTypeText.Short(Column("timestamp"), DatabaseType.SQLServer).ShouldBe("rowversion");
    }

    [Theory]
    [InlineData("INTEGER", "integer")]
    [InlineData("TEXT", "text")]
    [InlineData("REAL", "real")]
    [InlineData("DECIMAL(10, 2)", "decimal(10,2)")]
    [InlineData("VARCHAR( 255 )", "varchar(255)")]
    [InlineData("double precision", "double")]
    [InlineData("CHARACTER VARYING(20)", "varchar(20)")]
    [InlineData("datetime", "datetime")]
    [InlineData("", "")]
    public void Short_Sqlite_TidiesTheDeclaredType(string declared, string expected)
    {
        ColumnTypeText.Short(Column(declared), DatabaseType.WasmSQLite).ShouldBe(expected);
        ColumnTypeText.Short(Column(declared), DatabaseType.SQLite).ShouldBe(expected);
    }

    [Theory]
    [InlineData("ObjectId", "objectid")]
    [InlineData("String", "string")]
    [InlineData("Int32", "int32")]
    [InlineData("Mixed", "mixed")]
    public void Short_LiteDb_LowercasesTheInferredType(string dataType, string expected)
    {
        ColumnTypeText.Short(Column(dataType), DatabaseType.LiteDB).ShouldBe(expected);
    }

    [Theory]
    [InlineData("character varying", DatabaseType.PostgreSQL, "varchar")]
    [InlineData("character varying(20)", DatabaseType.PostgreSQL, "varchar(20)")]
    [InlineData("double precision", DatabaseType.WasmPostgreSQL, "double")]
    [InlineData("timestamp without time zone", DatabaseType.PostgreSQL, "timestamp")]
    [InlineData("integer[]", DatabaseType.PostgreSQL, "integer[]")]
    [InlineData("character varying[]", DatabaseType.WasmPostgreSQL, "varchar[]")]
    [InlineData("INTEGER", DatabaseType.WasmSQLite, "integer")]
    [InlineData("VARCHAR", DatabaseType.MySQL, "varchar")]
    [InlineData("INT UNSIGNED", DatabaseType.MySQL, "int unsigned")]
    [InlineData("nvarchar", DatabaseType.SQLServer, "nvarchar")]
    [InlineData("timestamp", DatabaseType.SQLServer, "rowversion")]
    [InlineData(null, DatabaseType.PostgreSQL, "")]
    public void Short_ResultColumnType_UsesTheSameCompactNames(string? type, DatabaseType engine, string expected)
    {
        ColumnTypeText.Short(type, engine).ShouldBe(expected);
    }

    [Fact]
    public void Describe_ListsFullTypeNullabilityKeysIdentityAndDefault()
    {
        var column = new ColumnInfo
        {
            Name = "id",
            DataType = "integer",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = true,
            DefaultValue = "nextval('products_id_seq'::regclass)"
        };

        ColumnTypeText.Describe(column, DatabaseType.PostgreSQL)
            .ShouldBe("integer · NOT NULL · primary key · identity · default nextval('products_id_seq'::regclass)");
    }

    [Fact]
    public void Describe_KeepsTheEnginesFullTypeName()
    {
        var column = new ColumnInfo { Name = "name", DataType = "character varying", MaxLength = 255, IsNullable = true };

        ColumnTypeText.Describe(column, DatabaseType.PostgreSQL).ShouldBe("character varying(255) · NULL");
    }

    [Fact]
    public void Describe_ShowsMaxForSqlServerUnboundedLength()
    {
        var column = new ColumnInfo { Name = "notes", DataType = "nvarchar", MaxLength = -1, IsNullable = true };

        ColumnTypeText.Describe(column, DatabaseType.SQLServer).ShouldBe("nvarchar(max) · NULL");
    }

    [Fact]
    public void Describe_NamesTheReferencedColumnOfAForeignKey()
    {
        var column = new ColumnInfo
        {
            Name = "category_id",
            DataType = "INTEGER",
            IsNullable = false,
            ForeignKey = new ForeignKeyInfo { ColumnName = "category_id", ReferencedTable = "categories", ReferencedColumn = "id" }
        };

        ColumnTypeText.Describe(column, DatabaseType.WasmSQLite).ShouldBe("INTEGER · NOT NULL · references categories(id)");
    }

    [Fact]
    public void Describe_QualifiesTheReferencedTableWithItsSchema()
    {
        var column = new ColumnInfo
        {
            Name = "category_id",
            DataType = "integer",
            IsNullable = true,
            ForeignKey = new ForeignKeyInfo
            {
                ColumnName = "category_id", ReferencedSchema = "public", ReferencedTable = "categories", ReferencedColumn = "id"
            }
        };

        ColumnTypeText.Describe(column, DatabaseType.PostgreSQL).ShouldBe("integer · NULL · references public.categories(id)");
    }

    [Fact]
    public void Describe_SaysWhenSqliteHasNoDeclaredType()
    {
        ColumnTypeText.Describe(Column(""), DatabaseType.WasmSQLite).ShouldBe("no declared type · NULL");
    }
}
