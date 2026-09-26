using Aion.Contracts.Queries;
using Aion.Core.Database.PostgreSQL;
using Npgsql;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class ProviderErrorMappingTests
{
    [Fact]
    public void PostgresException_UsesTheSqlStateAndMessageTextFromTheException()
    {
        // Arrange
        var exception = new PostgresException("column \"categry_id\" does not exist", "ERROR", "ERROR", "42703");

        // Act
        var error = PostgreSqlErrors.ToQueryError(exception, "SELECT categry_id FROM products");

        // Assert
        error.Raw.ShouldBe(exception.Message);
        error.Code.ShouldBe("SQLSTATE 42703");
        error.Message.ShouldBe("column \"categry_id\" does not exist");
        error.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        error.Token.ShouldBe("categry_id");
    }

    [Fact]
    public void NonPostgresException_FallsBackToItsMessage()
    {
        // Act
        var error = PostgreSqlErrors.ToQueryError(new InvalidOperationException("Connection refused"), "SELECT 1");

        // Assert
        error.Message.ShouldBe("Connection refused");
        error.Code.ShouldBeNull();
    }
}
