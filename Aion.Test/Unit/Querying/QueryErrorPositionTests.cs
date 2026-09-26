using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryErrorPositionTests
{
    private const string ProductsQuery = "SELECT name, price\nFROM products\nWHERE categry_id = 2\nORDER BY price DESC;";

    private static void ShouldBeAt(QueryError error, int line, int column, int endColumn)
    {
        error.Line.ShouldBe(line);
        error.Column.ShouldBe(column);
        error.EndColumn.ShouldBe(endColumn);
    }

    [Fact]
    public void SqliteUnknownColumn_IsLocatedFromTheNamedColumn()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(
            "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id", ProductsQuery);

        // Assert
        ShouldBeAt(error, line: 3, column: 7, endColumn: 17);
    }

    [Fact]
    public void SqliteUnknownTable_IsLocatedFromTheNamedTable()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("no such table: prodcts", "SELECT *\nFROM prodcts");

        // Assert
        ShouldBeAt(error, line: 2, column: 6, endColumn: 13);
    }

    [Fact]
    public void SqliteSyntaxError_IsLocatedWhenTheTokenAppearsOnce()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("near \"SELEC\": syntax error", "SELEC name FROM products");

        // Assert
        ShouldBeAt(error, line: 1, column: 1, endColumn: 6);
    }

    [Fact]
    public void SqliteSyntaxError_IsNotGuessedWhenTheTokenAppearsMoreThanOnce()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("near \",\": syntax error", "SELECT a, b,, c FROM t");

        // Assert
        error.Line.ShouldBeNull();
        error.Column.ShouldBeNull();
    }

    [Fact]
    public void Identifier_IsOnlyMatchedAsAWholeWord()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("no such column: id", "SELECT product_id, id FROM t");

        // Assert
        ShouldBeAt(error, line: 1, column: 20, endColumn: 22);
    }

    [Fact]
    public void QualifiedColumn_IsLocatedWithItsQualifier()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("no such column: p.categry_id", "SELECT p.categry_id FROM products p");

        // Assert
        ShouldBeAt(error, line: 1, column: 8, endColumn: 20);
    }

    [Fact]
    public void QuotedIdentifier_IsLocatedInsideItsQuotes()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("column \"categry_id\" does not exist", "SELECT \"categry_id\" FROM products");

        // Assert
        ShouldBeAt(error, line: 1, column: 9, endColumn: 19);
    }

    [Fact]
    public void PostgresPosition_IsConvertedToLineAndColumn()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("column \"categry_id\" does not exist", ProductsQuery,
            new EngineErrorDetails { Position = 40 });

        // Assert
        ShouldBeAt(error, line: 3, column: 7, endColumn: 17);
    }

    [Fact]
    public void PostgresPosition_CountsCharactersNotUtf16Units()
    {
        // Arrange: the emoji is one character to PostgreSQL but two UTF-16 units to .NET and Monaco.
        const string sql = "SELECT '😀', categry_id FROM t";

        // Act
        var error = QueryErrorNormalizer.Normalize("column \"categry_id\" does not exist", sql,
            new EngineErrorDetails { Position = 13 });

        // Assert
        ShouldBeAt(error, line: 1, column: 14, endColumn: 24);
    }

    [Fact]
    public void PostgresPosition_InALaterStatement_IsOffsetByWhereThatStatementStarts()
    {
        // Arrange
        const string sql = "SELECT 1;\nSELECT categry_id FROM t";

        // Act
        var error = QueryErrorNormalizer.Normalize("column \"categry_id\" does not exist", sql,
            new EngineErrorDetails { Position = 9, StatementOffset = 9 });

        // Assert
        ShouldBeAt(error, line: 2, column: 8, endColumn: 18);
    }

    [Fact]
    public void PostgresPositionPastTheEnd_MarksTheLastToken()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("syntax error at end of input", "SELECT name FROM",
            new EngineErrorDetails { Position = 17 });

        // Assert
        ShouldBeAt(error, line: 1, column: 13, endColumn: 17);
    }

    [Fact]
    public void SqlServerLine_IsCombinedWithTheTokenOnThatLine()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Invalid column name 'categry_id'.", ProductsQuery,
            new EngineErrorDetails { Line = 3 });

        // Assert
        ShouldBeAt(error, line: 3, column: 7, endColumn: 17);
    }

    [Fact]
    public void SqlServerStatementLine_FindsTheTokenFurtherIntoTheStatement()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Invalid column name 'categry_id'.", ProductsQuery,
            new EngineErrorDetails { Line = 1 });

        // Assert
        ShouldBeAt(error, line: 3, column: 7, endColumn: 17);
    }

    [Fact]
    public void LineWithoutAToken_MarksTheWholeLine()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Conversion failed when converting the varchar value 'x' to data type int.",
            "SELECT 1\n  WHERE a = 'x'  \n", new EngineErrorDetails { Line = 2 });

        // Assert
        ShouldBeAt(error, line: 2, column: 3, endColumn: 16);
    }

    [Fact]
    public void MySqlSyntaxError_IsLocatedFromTheTextItQuotes()
    {
        // Arrange
        const string sql = "SELECT name, price\nFROM products\nWHERE categry_id = = 2\nORDER BY price DESC";
        const string message = "You have an error in your SQL syntax; check the manual that corresponds to your MySQL server version " +
                               "for the right syntax to use near '= 2\nORDER BY price DESC' at line 3";

        // Act
        var error = QueryErrorNormalizer.Normalize(message, sql);

        // Assert
        error.Token.ShouldBe("=");
        ShouldBeAt(error, line: 3, column: 20, endColumn: 21);
    }

    [Fact]
    public void WithoutSql_OnlyTheEngineLineIsKept()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Invalid column name 'x'.", engine: new EngineErrorDetails { Line = 4 });

        // Assert
        error.Line.ShouldBe(4);
        error.Column.ShouldBeNull();
    }

    [Fact]
    public void GeneralError_WithoutPositionOrToken_HasNoLocation()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("division by zero", "SELECT 1/0");

        // Assert
        error.Line.ShouldBeNull();
    }

    [Fact]
    public void ShiftedTo_MovesPositionsFromASelectionIntoTheWholeEditor()
    {
        // Arrange
        var onFirstLine = QueryErrorNormalizer.Normalize("no such column: x", "SELECT x FROM t");
        var onSecondLine = QueryErrorNormalizer.Normalize("no such column: x", "SELECT 1,\n x FROM t");

        // Act
        var shiftedFirst = onFirstLine.ShiftedTo(line: 3, column: 10);
        var shiftedSecond = onSecondLine.ShiftedTo(line: 3, column: 10);

        // Assert
        ShouldBeAt(shiftedFirst, line: 3, column: 17, endColumn: 18);
        ShouldBeAt(shiftedSecond, line: 4, column: 2, endColumn: 3);
    }
}
