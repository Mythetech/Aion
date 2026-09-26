using System.Globalization;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Shouldly;

namespace Aion.Test.Unit.Editing;

public class SqlDialectTests
{
    private const string Injection = "'; DROP TABLE x; --";

    private static readonly DateTime LocalTimestamp = new(2024, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified);

    public static TheoryData<SqlDialect, object?, string> Literals => new()
    {
        { PostgreSqlDialect.Instance, "O'Brien's Hub", "'O''Brien''s Hub'" },
        { PostgreSqlDialect.Instance, @"C:\temp\'x", @"E'C:\\temp\\''x'" },
        { PostgreSqlDialect.Instance, Injection, "'''; DROP TABLE x; --'" },
        { PostgreSqlDialect.Instance, null, "NULL" },
        { PostgreSqlDialect.Instance, DBNull.Value, "NULL" },
        { PostgreSqlDialect.Instance, new byte[] { 0x01, 0xAB }, @"'\x01AB'::bytea" },
        { PostgreSqlDialect.Instance, true, "TRUE" },
        { PostgreSqlDialect.Instance, false, "FALSE" },
        { PostgreSqlDialect.Instance, 0, "0" },
        { PostgreSqlDialect.Instance, -42L, "-42" },
        { PostgreSqlDialect.Instance, LocalTimestamp, "'2024-01-02T03:04:05.123'" },
        { PostgreSqlDialect.Instance, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), "'2024-01-02T03:04:05Z'" },

        { SqliteDialect.Instance, "O'Brien's Hub", "'O''Brien''s Hub'" },
        { SqliteDialect.Instance, @"C:\temp\'x", @"'C:\temp\''x'" },
        { SqliteDialect.Instance, Injection, "'''; DROP TABLE x; --'" },
        { SqliteDialect.Instance, null, "NULL" },
        { SqliteDialect.Instance, new byte[] { 0x01, 0xAB }, "X'01AB'" },
        { SqliteDialect.Instance, true, "1" },
        { SqliteDialect.Instance, false, "0" },
        { SqliteDialect.Instance, LocalTimestamp, "'2024-01-02 03:04:05.123'" },

        { MySqlDialect.Instance, "O'Brien's Hub", "'O''Brien''s Hub'" },
        { MySqlDialect.Instance, @"C:\temp", @"'C:\\temp'" },
        { MySqlDialect.Instance, @"\'; DROP TABLE x; --", @"'\\''; DROP TABLE x; --'" },
        { MySqlDialect.Instance, Injection, "'''; DROP TABLE x; --'" },
        { MySqlDialect.Instance, "a\0b", @"'a\0b'" },
        { MySqlDialect.Instance, null, "NULL" },
        { MySqlDialect.Instance, new byte[] { 0x01, 0xAB }, "X'01AB'" },
        { MySqlDialect.Instance, true, "TRUE" },
        { MySqlDialect.Instance, 0, "0" },
        { MySqlDialect.Instance, LocalTimestamp, "'2024-01-02 03:04:05.123'" },

        { SqlServerDialect.Instance, "O'Brien's Hub", "N'O''Brien''s Hub'" },
        { SqlServerDialect.Instance, @"C:\temp\'x", @"N'C:\temp\''x'" },
        { SqlServerDialect.Instance, Injection, "N'''; DROP TABLE x; --'" },
        { SqlServerDialect.Instance, null, "NULL" },
        { SqlServerDialect.Instance, new byte[] { 0x01, 0xAB }, "0x01AB" },
        { SqlServerDialect.Instance, true, "1" },
        { SqlServerDialect.Instance, false, "0" },
        { SqlServerDialect.Instance, LocalTimestamp, "N'2024-01-02T03:04:05.123'" },
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void FormatLiteral_RendersEngineSpecificLiteral(SqlDialect dialect, object? value, string expected)
    {
        dialect.FormatLiteral(value).ShouldBe(expected);
    }

    public static TheoryData<SqlDialect> Dialects => new()
    {
        PostgreSqlDialect.Instance,
        SqliteDialect.Instance,
        MySqlDialect.Instance,
        SqlServerDialect.Instance
    };

    [Theory]
    [MemberData(nameof(Dialects))]
    public void FormatLiteral_NumbersIgnoreCurrentCulture(SqlDialect dialect)
    {
        var (decimalText, doubleText, floatText) = WithCulture("de-DE", () => (
            dialect.FormatLiteral(1234.5m),
            dialect.FormatLiteral(-0.25d),
            dialect.FormatLiteral(0.1f)));

        decimalText.ShouldBe("1234.5");
        doubleText.ShouldBe("-0.25");
        floatText.ShouldBe("0.1");
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void FormatLiteral_DatesIgnoreCurrentCulture(SqlDialect dialect)
    {
        // th-TH uses the Buddhist calendar, where 2024 is 2567.
        var (dateTime, date) = WithCulture("th-TH", () => (
            dialect.FormatLiteral(new DateTime(2024, 12, 31, 8, 0, 0)),
            dialect.FormatLiteral(new DateOnly(2024, 12, 31))));

        dateTime.ShouldContain("2024-12-31");
        date.ShouldEndWith("2024-12-31'");
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void FormatLiteral_NonFiniteDoublesAreQuoted(SqlDialect dialect)
    {
        dialect.FormatLiteral(double.NaN).ShouldEndWith("NaN'");
    }

    [Fact]
    public void FormatLiteral_GuidAndTime()
    {
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        PostgreSqlDialect.Instance.FormatLiteral(id).ShouldBe("'0f8fad5b-d9cb-469f-a165-70867728950e'");
        SqlServerDialect.Instance.FormatLiteral(new TimeSpan(0, 1, 2, 3, 400)).ShouldBe("N'01:02:03.4'");
        MySqlDialect.Instance.FormatLiteral(new TimeOnly(23, 59, 1)).ShouldBe("'23:59:01'");
    }

    public static TheoryData<SqlDialect, string, string> Identifiers => new()
    {
        { PostgreSqlDialect.Instance, "we\"ird", "\"we\"\"ird\"" },
        { SqliteDialect.Instance, "we\"ird", "\"we\"\"ird\"" },
        { MySqlDialect.Instance, "we`ird", "`we``ird`" },
        { SqlServerDialect.Instance, "we]ird", "[we]]ird]" },
    };

    [Theory]
    [MemberData(nameof(Identifiers))]
    public void QuoteIdentifier_EscapesTheClosingQuote(SqlDialect dialect, string identifier, string expected)
    {
        dialect.QuoteIdentifier(identifier).ShouldBe(expected);
    }

    [Fact]
    public void BuildKeyPredicate_JoinsEveryKeyColumn()
    {
        var predicate = MySqlDialect.Instance.BuildKeyPredicate(
        [
            new ColumnValue("tenant", "a'b"),
            new ColumnValue("id", 0)
        ]);

        predicate.ShouldBe("`tenant` = 'a''b' AND `id` = 0");
    }

    [Fact]
    public void BuildKeyPredicate_NullKeyUsesIsNull()
    {
        PostgreSqlDialect.Instance.BuildKeyPredicate([new ColumnValue("id", null)]).ShouldBe("\"id\" IS NULL");
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void BuildKeyPredicate_WithoutKeys_Throws(SqlDialect dialect)
    {
        Should.Throw<ArgumentException>(() => dialect.BuildKeyPredicate([]));
    }

    [Theory]
    [MemberData(nameof(Dialects))]
    public void BuildAssignments_WithoutValues_Throws(SqlDialect dialect)
    {
        Should.Throw<ArgumentException>(() => dialect.BuildAssignments([]));
    }

    public static TheoryData<SqlDialect, string?, string> QualifiedTables => new()
    {
        { PostgreSqlDialect.Instance, "public", "\"public\".\"orders\"" },
        { SqlServerDialect.Instance, "dbo", "[dbo].[orders]" },
        { MySqlDialect.Instance, "shop", "`shop`.`orders`" },
        { SqliteDialect.Instance, null, "\"orders\"" },
        { PostgreSqlDialect.Instance, "", "\"orders\"" },
    };

    [Theory]
    [MemberData(nameof(QualifiedTables))]
    public void QualifyTable_QuotesTheSchemaOnlyWhenThereIsOne(SqlDialect dialect, string? schema, string expected)
    {
        dialect.QualifyTable(schema, "orders").ShouldBe(expected);
    }

    public static TheoryData<SqlDialect, string> FirstRowsSelects => new()
    {
        { PostgreSqlDialect.Instance, "SELECT * FROM \"orders\"\nLIMIT 1000;" },
        { SqliteDialect.Instance, "SELECT * FROM \"orders\"\nLIMIT 1000;" },
        { MySqlDialect.Instance, "SELECT * FROM `orders`\nLIMIT 1000;" },
        { SqlServerDialect.Instance, "SELECT TOP (1000) * FROM [orders];" },
    };

    [Theory]
    [MemberData(nameof(FirstRowsSelects))]
    public void SelectRows_LimitsRowsTheWayTheEngineDoes(SqlDialect dialect, string expected)
    {
        dialect.SelectRows(dialect.QuoteIdentifier("orders"), limit: 1000).ShouldBe(expected);
    }

    [Fact]
    public void SelectRows_WithAPredicateAndNoLimit_FiltersEveryRow()
    {
        var dialect = PostgreSqlDialect.Instance;

        var sql = dialect.SelectRows(dialect.QualifyTable("public", "customers"),
            dialect.BuildKeyPredicate([new ColumnValue("code", "O'Brien")]));

        sql.ShouldBe("SELECT * FROM \"public\".\"customers\"\nWHERE \"code\" = 'O''Brien';");
    }

    [Fact]
    public void SelectRows_OnSqlServer_PutsTopBeforeTheColumnsAndTheFilterAfterTheTable()
    {
        var dialect = SqlServerDialect.Instance;

        var sql = dialect.SelectRows("[dbo].[customers]", dialect.BuildKeyPredicate([new ColumnValue("id", 7)]), 1);

        sql.ShouldBe("SELECT TOP (1) * FROM [dbo].[customers]\nWHERE [id] = 7;");
    }

    private static T WithCulture<T>(string culture, Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
