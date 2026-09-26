using System.Data;
using Aion.Contracts.Queries;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryResultReaderTests
{
    private static DataTable Products()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("price", typeof(double));
        table.Rows.Add(1, 2.5);
        table.Rows.Add(2, DBNull.Value);
        return table;
    }

    [Fact]
    public async Task ReadAsync_ReadsColumnsTypesAndRows()
    {
        // Arrange
        var result = new QueryResult();

        // Act
        await QueryResultReader.ReadAsync(Products().CreateDataReader(), result, CancellationToken.None);

        // Assert
        result.Columns.ShouldBe(["id", "price"]);
        result.ColumnTypes.ShouldBe(["Int32", "Double"]);
        result.Rows.Count.ShouldBe(2);
        result.Rows[0]["price"].ShouldBe(2.5);
    }

    [Fact]
    public async Task ReadAsync_TurnsDatabaseNullsIntoNull()
    {
        var result = new QueryResult();

        await QueryResultReader.ReadAsync(Products().CreateDataReader(), result, CancellationToken.None);

        result.Rows[1]["price"].ShouldBeNull();
    }

    [Fact]
    public void Clone_KeepsTheColumnTypes()
    {
        var result = new QueryResult { Columns = ["id"], ColumnTypes = ["integer"] };

        result.Clone().ColumnTypes.ShouldBe(["integer"]);
    }

    [Fact]
    public void ColumnType_IsNullWhenTheProviderReportedNone()
    {
        var result = new QueryResult { Columns = ["id", "name"], ColumnTypes = ["integer"] };

        result.ColumnType(0).ShouldBe("integer");
        result.ColumnType(1).ShouldBeNull();
    }
}
