using Aion.Core.Database.PostgreSQL;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class PostgreSqlStatementStartsTests
{
    [Theory]
    [InlineData("SELECT 1;\n  SELECT 'x'::int", 1, 12)]
    [InlineData("SELECT ';';SELECT 2", 1, 11)]
    [InlineData("SELECT $$a;b$$; SELECT 2", 1, 16)]
    [InlineData("SELECT $fn$a;b$fn$; SELECT 2", 1, 20)]
    [InlineData("SELECT \"a;b\" FROM t; SELECT 2", 1, 21)]
    [InlineData("SELECT E'it\\'s;'; SELECT 2", 1, 18)]
    [InlineData("SELECT 1; -- a; b\nSELECT 2", 1, 10)]
    [InlineData("/* a; /* b; */ c; */ SELECT 1; SELECT 2", 1, 31)]
    [InlineData("SELECT 1; SELECT 2; SELECT 3", 2, 20)]
    [InlineData("\n  SELECT 1", 0, 0)]
    public void StartOf_FindsWhereNpgsqlStartsTheStatement(string sql, int statementIndex, int expected)
    {
        // Act
        var start = PostgreSqlStatementStarts.StartOf(sql, statementIndex);

        // Assert
        start.ShouldBe(expected);
    }

    [Theory]
    [InlineData("SELECT 1;", 1)]
    [InlineData("SELECT 1; SELECT 2", 2)]
    [InlineData("SELECT 1", -1)]
    public void StartOf_ForAStatementThatIsNotThere_IsNull(string sql, int statementIndex)
    {
        // Act
        var start = PostgreSqlStatementStarts.StartOf(sql, statementIndex);

        // Assert
        start.ShouldBeNull();
    }
}
