using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryErrorNormalizerTests
{
    [Fact]
    public void SqliteWorkerError_StripsDriverPrefixesAndKeepsTheResultCodeSeparately()
    {
        // Arrange
        const string raw = "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id";

        // Act
        var error = QueryErrorNormalizer.Normalize(raw);

        // Assert
        error.Raw.ShouldBe(raw);
        error.Message.ShouldBe("no such column: categry_id");
        error.Code.ShouldBe("SQLITE_ERROR (code 1)");
        error.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        error.Title.ShouldBe("No such column");
        error.Token.ShouldBe("categry_id");
    }

    [Fact]
    public void SqliteUnknownTable_IsClassifiedWithItsName()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Worker error: SQLITE_ERROR: sqlite3 result code 1: no such table: prodcts");

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.UnknownTable);
        error.Title.ShouldBe("No such table");
        error.Token.ShouldBe("prodcts");
    }

    [Fact]
    public void SqliteSyntaxError_NamesTheTokenItFailedNear()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Worker error: SQLITE_ERROR: sqlite3 result code 1: near \"SELEC\": syntax error");

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.Syntax);
        error.Title.ShouldBe("Syntax error near");
        error.Token.ShouldBe("SELEC");
    }

    [Fact]
    public void SqliteExtendedResultCode_IsKeptWithItsNumber()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(
            "Worker error: SQLITE_CONSTRAINT_PRIMARYKEY: sqlite3 result code 1555: UNIQUE constraint failed: products.id");

        // Assert
        error.Code.ShouldBe("SQLITE_CONSTRAINT_PRIMARYKEY (code 1555)");
        error.Message.ShouldBe("UNIQUE constraint failed: products.id");
        error.Kind.ShouldBe(QueryErrorKind.General);
        error.Title.ShouldBe("UNIQUE constraint failed: products.id");
        error.Token.ShouldBeNull();
    }

    [Fact]
    public void NpgsqlMessage_StripsTheSqlStatePrefix()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("42703: column \"categry_id\" does not exist");

        // Assert
        error.Message.ShouldBe("column \"categry_id\" does not exist");
        error.Code.ShouldBe("SQLSTATE 42703");
        error.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        error.Token.ShouldBe("categry_id");
    }

    [Fact]
    public void EngineDetails_TakePrecedenceOverTextParsedFromTheRawMessage()
    {
        // Arrange
        const string raw = "42P01: relation \"prodcts\" does not exist\n\nPOSITION: 15";

        // Act
        var error = QueryErrorNormalizer.Normalize(raw, engine: new EngineErrorDetails
        {
            Message = "relation \"prodcts\" does not exist",
            Code = "SQLSTATE 42P01"
        });

        // Assert
        error.Raw.ShouldBe(raw);
        error.Message.ShouldBe("relation \"prodcts\" does not exist");
        error.Code.ShouldBe("SQLSTATE 42P01");
        error.Kind.ShouldBe(QueryErrorKind.UnknownTable);
        error.Token.ShouldBe("prodcts");
    }

    [Theory]
    [InlineData("column p.categry_id does not exist", "p.categry_id")]
    [InlineData("column \"categry_id\" of relation \"products\" does not exist", "categry_id")]
    [InlineData("Unknown column 'categry_id' in 'where clause'", "categry_id")]
    [InlineData("Invalid column name 'categry_id'.", "categry_id")]
    public void UnknownColumnMessages_FromEachEngine_AreRecognized(string message, string token)
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(message);

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        error.Token.ShouldBe(token);
        error.Title.ShouldBe("No such column");
    }

    [Theory]
    [InlineData("Table 'sample_store.prodcts' doesn't exist", "sample_store.prodcts")]
    [InlineData("Invalid object name 'prodcts'.", "prodcts")]
    public void UnknownTableMessages_FromEachEngine_AreRecognized(string message, string token)
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(message);

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.UnknownTable);
        error.Token.ShouldBe(token);
    }

    [Theory]
    [InlineData("syntax error at or near \"SELEC\"", "SELEC")]
    [InlineData("Incorrect syntax near 'SELEC'.", "SELEC")]
    [InlineData("Incorrect syntax near the keyword 'FROM'.", "FROM")]
    [InlineData("You have an error in your SQL syntax; check the manual that corresponds to your MySQL server version for the right syntax to use near 'SELEC name FROM products' at line 1", "SELEC")]
    public void SyntaxErrors_FromEachEngine_NameTheTokenTheyFailedNear(string message, string token)
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(message);

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.Syntax);
        error.Token.ShouldBe(token);
        error.Title.ShouldBe("Syntax error near");
    }

    [Theory]
    [InlineData("syntax error at end of input")]
    [InlineData("You have an error in your SQL syntax; check the manual that corresponds to your MySQL server version for the right syntax to use near '' at line 1")]
    public void SyntaxErrorsAtTheEnd_HaveNoToken(string message)
    {
        // Act
        var error = QueryErrorNormalizer.Normalize(message);

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.Syntax);
        error.Token.ShouldBeNull();
        error.Title.ShouldBe("Syntax error at end of input");
    }

    [Fact]
    public void JavaScriptStackTrace_IsDroppedFromTheMessage()
    {
        // Arrange: JS errors reach .NET as the message, a newline, then the stack.
        const string raw = "Database 'shop' not found\nError: Database 'shop' not found\n    at query (https://localhost/js/pglite-interop.js:12:25)\n    at async run (https://localhost/js/pglite-interop.js:40:9)";

        // Act
        var error = QueryErrorNormalizer.Normalize(raw);

        // Assert
        error.Message.ShouldBe("Database 'shop' not found");
        error.Title.ShouldBe("Database 'shop' not found");
        error.Raw.ShouldBe(raw);
    }

    [Fact]
    public void UnrecognizedMessage_IsUsedAsTheTitleWithACapitalLetter()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("division by zero");

        // Assert
        error.Kind.ShouldBe(QueryErrorKind.General);
        error.Title.ShouldBe("Division by zero");
        error.Code.ShouldBeNull();
    }

    [Fact]
    public void MultiLineMessage_UsesItsFirstLineAsTheTitle()
    {
        // Act
        var error = QueryErrorNormalizer.Normalize("Invalid column name 'a'.\nInvalid column name 'b'.");

        // Assert
        error.Token.ShouldBe("a");
        error.Message.ShouldBe("Invalid column name 'a'.\nInvalid column name 'b'.");
    }

    [Fact]
    public void QueryResult_SetError_KeepsTheRawTextInError()
    {
        // Arrange
        var result = new QueryResult();
        var error = QueryErrorNormalizer.Normalize("Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: x");

        // Act
        result.SetError(error);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe(error.Raw);
        result.ErrorDetail.ShouldBe(error);
        result.Clone().ErrorDetail.ShouldBe(error);
    }
}
