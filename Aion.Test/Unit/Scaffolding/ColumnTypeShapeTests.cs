using Aion.Components.Scaffolding;
using Aion.Contracts.Database;
using Shouldly;

namespace Aion.Test.Unit.Scaffolding;

public class ColumnTypeShapeTests
{
    [Theory]
    [InlineData("int4", DatabaseType.PostgreSQL, ColumnTypeFamily.Integer)]
    [InlineData("integer", DatabaseType.WasmPostgreSQL, ColumnTypeFamily.Integer)]
    [InlineData("INTEGER", DatabaseType.WasmSQLite, ColumnTypeFamily.Integer)]
    [InlineData("bigserial", DatabaseType.PostgreSQL, ColumnTypeFamily.Integer)]
    [InlineData("int unsigned", DatabaseType.MySQL, ColumnTypeFamily.Integer)]
    [InlineData("int(10) unsigned", DatabaseType.MySQL, ColumnTypeFamily.Integer)]
    [InlineData("UNSIGNED BIG INT", DatabaseType.WasmSQLite, ColumnTypeFamily.Integer)]
    [InlineData("tinyint", DatabaseType.MySQL, ColumnTypeFamily.Integer)]
    [InlineData("tinyint(1)", DatabaseType.MySQL, ColumnTypeFamily.Boolean)]
    [InlineData("bool", DatabaseType.PostgreSQL, ColumnTypeFamily.Boolean)]
    [InlineData("boolean", DatabaseType.MySQL, ColumnTypeFamily.Boolean)]
    [InlineData("BOOLEAN", DatabaseType.WasmSQLite, ColumnTypeFamily.Boolean)]
    [InlineData("bit", DatabaseType.SQLServer, ColumnTypeFamily.Boolean)]
    [InlineData("bit", DatabaseType.MySQL, ColumnTypeFamily.Boolean)]
    [InlineData("bit", DatabaseType.PostgreSQL, ColumnTypeFamily.Unknown)]
    [InlineData("numeric", DatabaseType.PostgreSQL, ColumnTypeFamily.Decimal)]
    [InlineData("decimal(10,2)", DatabaseType.MySQL, ColumnTypeFamily.Decimal)]
    [InlineData("money", DatabaseType.SQLServer, ColumnTypeFamily.Decimal)]
    [InlineData("NUMERIC", DatabaseType.WasmSQLite, ColumnTypeFamily.Decimal)]
    [InlineData("double precision", DatabaseType.PostgreSQL, ColumnTypeFamily.Float)]
    [InlineData("float8", DatabaseType.PostgreSQL, ColumnTypeFamily.Float)]
    [InlineData("REAL", DatabaseType.WasmSQLite, ColumnTypeFamily.Float)]
    [InlineData("float", DatabaseType.SQLServer, ColumnTypeFamily.Float)]
    [InlineData("text", DatabaseType.PostgreSQL, ColumnTypeFamily.Text)]
    [InlineData("character varying", DatabaseType.PostgreSQL, ColumnTypeFamily.Text)]
    [InlineData("varchar(255)", DatabaseType.MySQL, ColumnTypeFamily.Text)]
    [InlineData("nvarchar", DatabaseType.SQLServer, ColumnTypeFamily.Text)]
    [InlineData("longtext", DatabaseType.MySQL, ColumnTypeFamily.Text)]
    [InlineData("VARCHAR( 20 )", DatabaseType.WasmSQLite, ColumnTypeFamily.Text)]
    [InlineData("date", DatabaseType.PostgreSQL, ColumnTypeFamily.Date)]
    [InlineData("timestamp", DatabaseType.PostgreSQL, ColumnTypeFamily.DateTime)]
    [InlineData("timestamp without time zone", DatabaseType.PostgreSQL, ColumnTypeFamily.DateTime)]
    [InlineData("timestamptz", DatabaseType.WasmPostgreSQL, ColumnTypeFamily.DateTimeOffset)]
    [InlineData("timestamp with time zone", DatabaseType.PostgreSQL, ColumnTypeFamily.DateTimeOffset)]
    [InlineData("datetime", DatabaseType.MySQL, ColumnTypeFamily.DateTime)]
    [InlineData("timestamp", DatabaseType.MySQL, ColumnTypeFamily.DateTime)]
    [InlineData("datetime2", DatabaseType.SQLServer, ColumnTypeFamily.DateTime)]
    [InlineData("datetimeoffset", DatabaseType.SQLServer, ColumnTypeFamily.DateTimeOffset)]
    [InlineData("DATETIME", DatabaseType.WasmSQLite, ColumnTypeFamily.DateTime)]
    [InlineData("time", DatabaseType.PostgreSQL, ColumnTypeFamily.Time)]
    [InlineData("time without time zone", DatabaseType.PostgreSQL, ColumnTypeFamily.Time)]
    [InlineData("interval", DatabaseType.PostgreSQL, ColumnTypeFamily.Interval)]
    [InlineData("uuid", DatabaseType.PostgreSQL, ColumnTypeFamily.Uuid)]
    [InlineData("uniqueidentifier", DatabaseType.SQLServer, ColumnTypeFamily.Uuid)]
    [InlineData("jsonb", DatabaseType.PostgreSQL, ColumnTypeFamily.Json)]
    [InlineData("json", DatabaseType.MySQL, ColumnTypeFamily.Json)]
    [InlineData("bytea", DatabaseType.PostgreSQL, ColumnTypeFamily.Binary)]
    [InlineData("varbinary", DatabaseType.SQLServer, ColumnTypeFamily.Binary)]
    [InlineData("BLOB", DatabaseType.WasmSQLite, ColumnTypeFamily.Binary)]
    [InlineData("timestamp", DatabaseType.SQLServer, ColumnTypeFamily.RowVersion)]
    [InlineData("rowversion", DatabaseType.SQLServer, ColumnTypeFamily.RowVersion)]
    [InlineData("ARRAY", DatabaseType.PostgreSQL, ColumnTypeFamily.Unknown)]
    [InlineData("integer[]", DatabaseType.PostgreSQL, ColumnTypeFamily.Unknown)]
    [InlineData("USER-DEFINED", DatabaseType.PostgreSQL, ColumnTypeFamily.Unknown)]
    [InlineData("enum", DatabaseType.MySQL, ColumnTypeFamily.Unknown)]
    [InlineData("", DatabaseType.WasmSQLite, ColumnTypeFamily.Unknown)]
    public void Of_RecognisesTheTypeUnderEachEnginesSpelling(string dataType, DatabaseType engine, ColumnTypeFamily expected)
    {
        ColumnTypeShape.Of(dataType, engine).Family.ShouldBe(expected);
    }

    [Theory]
    [InlineData("varchar(255)", null, 255)]
    [InlineData("character varying(12)", null, 12)]
    [InlineData("character varying", 40, 40)]
    [InlineData("nvarchar", 50, 50)]
    [InlineData("nvarchar(max)", null, null)]
    [InlineData("nvarchar", -1, null)]
    [InlineData("char(2)", null, 2)]
    [InlineData("text", null, null)]
    public void Of_ReadsTheMaximumLengthFromTheTypeOrTheCatalog(string dataType, int? catalogLength, int? expected)
    {
        ColumnTypeShape.Of(dataType, DatabaseType.PostgreSQL, catalogLength).MaxLength.ShouldBe(expected);
    }

    [Theory]
    [InlineData("tinyint", DatabaseType.MySQL, 0L, 127L)]
    [InlineData("tinyint", DatabaseType.SQLServer, 0L, 127L)]
    [InlineData("smallint", DatabaseType.PostgreSQL, -32768L, 32767L)]
    [InlineData("int", DatabaseType.SQLServer, int.MinValue, int.MaxValue)]
    [InlineData("INTEGER", DatabaseType.WasmSQLite, long.MinValue, long.MaxValue)]
    [InlineData("numeric(5,2)", DatabaseType.PostgreSQL, -999L, 999L)]
    public void Of_KnowsTheRangeOfWholeNumbersTheColumnHolds(string dataType, DatabaseType engine, long min, long max)
    {
        var shape = ColumnTypeShape.Of(dataType, engine);

        shape.MinInteger.ShouldBe(min);
        shape.MaxInteger.ShouldBe(max);
    }
}
