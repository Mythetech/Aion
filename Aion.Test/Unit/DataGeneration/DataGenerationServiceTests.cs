using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.DataGeneration;

public class DataGenerationServiceTests
{
    private readonly DataGenerationService _sut = new();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();

    public DataGenerationServiceTests()
    {
        _provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());
    }

    [Fact]
    public async Task Generate_ShouldExecuteInsertStatements()
    {
        var model = CreateModel(50);

        await _sut.GenerateAsync(model, _provider, "conn", "db");

        await _provider.Received(1).ExecuteQueryAsync("conn", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_ShouldBatchLargeRowCounts()
    {
        var model = CreateModel(500);

        await _sut.GenerateAsync(model, _provider, "conn", "db");

        await _provider.Received(3).ExecuteQueryAsync("conn", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_ShouldReportProgress()
    {
        var model = CreateModel(500);
        var progressValues = new List<int>();
        var progress = new Progress<int>(v => progressValues.Add(v));

        await _sut.GenerateAsync(model, _provider, "conn", "db", progress);

        await Task.Delay(200);
        progressValues.ShouldNotBeEmpty();
        progressValues.Max().ShouldBe(500);
    }

    [Fact]
    public async Task Generate_ShouldReturnTotalRowCount()
    {
        var model = CreateModel(150);

        var result = await _sut.GenerateAsync(model, _provider, "conn", "db");

        result.ShouldBe(150);
    }

    [Fact]
    public async Task Generate_SqlShouldContainColumnNames()
    {
        var model = CreateModel(5);
        string? capturedSql = null;
        _provider.ExecuteQueryAsync("conn", Arg.Do<string>(s => capturedSql = s), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());

        await _sut.GenerateAsync(model, _provider, "conn", "db");

        capturedSql.ShouldNotBeNull();
        capturedSql.ShouldContain("\"name\"");
        capturedSql.ShouldContain("INSERT INTO");
        capturedSql.ShouldContain("\"test_table\"");
    }

    [Fact]
    public async Task Generate_ShouldSkipIdentityColumns()
    {
        var model = new DataGenerationModel
        {
            TableName = "users",
            RowCount = 5,
            ColumnGenerators =
            [
                new ColumnGeneratorBinding
                {
                    Column = new ColumnInfo { Name = "id", DataType = "integer", IsIdentity = true },
                    Generator = null
                },
                new ColumnGeneratorBinding
                {
                    Column = new ColumnInfo { Name = "name", DataType = "text" },
                    Generator = new RandomTextGenerator(),
                    Options = new DataGeneratorOptions()
                }
            ]
        };

        string? capturedSql = null;
        _provider.ExecuteQueryAsync("conn", Arg.Do<string>(s => capturedSql = s), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());

        await _sut.GenerateAsync(model, _provider, "conn", "db");

        capturedSql.ShouldNotBeNull();
        capturedSql.ShouldNotContain("\"id\"");
        capturedSql.ShouldContain("\"name\"");
    }

    [Fact]
    public async Task Generate_NullValues_ShouldOutputNullLiteral()
    {
        var model = new DataGenerationModel
        {
            TableName = "test",
            RowCount = 1,
            ColumnGenerators =
            [
                new ColumnGeneratorBinding
                {
                    Column = new ColumnInfo { Name = "notes", DataType = "text" },
                    Generator = new NullGenerator(),
                    Options = new DataGeneratorOptions()
                }
            ]
        };

        string? capturedSql = null;
        _provider.ExecuteQueryAsync("conn", Arg.Do<string>(s => capturedSql = s), Arg.Any<CancellationToken>())
            .Returns(new QueryResult());

        await _sut.GenerateAsync(model, _provider, "conn", "db");

        capturedSql.ShouldNotBeNull();
        capturedSql.ShouldContain("NULL");
    }

    [Fact]
    public async Task Generate_ShouldRespectCancellation()
    {
        var model = CreateModel(1000);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.GenerateAsync(model, _provider, "conn", "db", cancellationToken: cts.Token));
    }

    private static DataGenerationModel CreateModel(int rowCount)
    {
        return new DataGenerationModel
        {
            TableName = "test_table",
            RowCount = rowCount,
            ColumnGenerators =
            [
                new ColumnGeneratorBinding
                {
                    Column = new ColumnInfo { Name = "name", DataType = "text" },
                    Generator = new RandomTextGenerator(),
                    Options = new DataGeneratorOptions()
                }
            ]
        };
    }
}
