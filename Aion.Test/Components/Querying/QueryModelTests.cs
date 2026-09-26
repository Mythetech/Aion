using Aion.Components.Querying;
using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryModelTests
{
    [Fact]
    public void SetResult_WithOnlyErrorText_AddsTheNormalizedError()
    {
        // Arrange
        var query = new QueryModel { Query = "SELECT categry_id FROM products" };
        var result = new QueryResult { Error = "Worker error: SQLITE_ERROR: sqlite3 result code 1: no such column: categry_id" };

        // Act
        query.SetResult(result);

        // Assert
        query.Result!.ErrorDetail.ShouldNotBeNull();
        query.Result.ErrorDetail.Message.ShouldBe("no such column: categry_id");
        query.Result.ErrorDetail.Code.ShouldBe("SQLITE_ERROR (code 1)");
    }

    [Fact]
    public void SetResult_KeepsTheProvidersStructuredError()
    {
        // Arrange
        var query = new QueryModel { Query = "SELECT 1" };
        var providerError = new QueryError { Raw = "raw", Message = "message", Title = "Title", Code = "SQLSTATE 42703" };
        var result = new QueryResult();
        result.SetError(providerError);

        // Act
        query.SetResult(result);

        // Assert
        query.Result!.ErrorDetail.ShouldBeSameAs(providerError);
    }

    [Fact]
    public void SetResult_ForASuccessfulResult_HasNoError()
    {
        // Arrange
        var query = new QueryModel { Query = "SELECT 1" };

        // Act
        query.SetResult(new QueryResult());

        // Assert
        query.Result!.ErrorDetail.ShouldBeNull();
    }
}
