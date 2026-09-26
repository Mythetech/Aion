using Aion.Components.Scaffolding.DataGeneration;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Contracts.Queries;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Aion.Test.Unit.DataGeneration;

public class DataGenerationServiceTests
{
    private readonly DataGenerationService _sut = new();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider, ISqlDialectProvider>();
    private readonly TransactionInfo _transaction = new();
    private readonly List<string> _executed = [];
    private readonly Dictionary<string, Func<QueryResult>> _responses = [];

    public DataGenerationServiceTests()
    {
        UseDialect(PostgreSqlDialect.Instance, DatabaseType.PostgreSQL);
        _provider.BeginTransactionAsync("conn").Returns(_transaction);
        _provider.ExecuteInTransactionAsync("conn", Arg.Any<string>(), _transaction.Id, Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var sql = ci.ArgAt<string>(1);
                _executed.Add(sql);
                var response = _responses.FirstOrDefault(r => sql.StartsWith(r.Key, StringComparison.Ordinal)).Value;
                return response?.Invoke() ?? new QueryResult();
            });
    }

    private void UseDialect(SqlDialect dialect, DatabaseType engine)
    {
        ((ISqlDialectProvider)_provider).Dialect.Returns(dialect);
        _provider.DatabaseType.Returns(engine);
    }

    private IEnumerable<string> Inserts => _executed.Where(sql => sql.StartsWith("INSERT", StringComparison.Ordinal));

    private DataGenerationModel Model(int rows, params ColumnInfo[] columns) => new()
    {
        TableName = "orders",
        Schema = "public",
        RowCount = rows,
        ColumnGenerators = DataGenerationPlan.CreateBindings(columns, _provider.DatabaseType)
    };

    private static ColumnInfo Text(string name) => new() { Name = name, DataType = "text", IsNullable = true };

    private static void Choose(DataGenerationModel model, string column, IDataGenerator generator, string? values = null)
    {
        var binding = model.ColumnGenerators.Single(b => b.Column.Name == column);
        binding.Generator = generator;
        binding.Options.CustomValues = values;
    }

    private static QueryResult Failure(string message)
    {
        var result = new QueryResult();
        result.SetError(new QueryError { Raw = $"23505: {message}", Message = message, Title = "Error" });
        return result;
    }

    private static QueryResult Rows(string column, params object[] values)
    {
        var result = new QueryResult();
        var key = result.AddColumn(column);
        result.Rows.AddRange(values.Select(v => new Dictionary<string, object> { [key] = v }));
        return result;
    }

    [Fact]
    public async Task Generate_InsertsEveryRowInOneTransactionAndCommits()
    {
        var result = await _sut.GenerateAsync(Model(50, Text("notes")), _provider, "conn");

        result.Error.ShouldBeNull();
        result.RowsInserted.ShouldBe(50);
        Inserts.ShouldHaveSingleItem();
        await _provider.Received(1).CommitTransactionAsync("conn", _transaction.Id);
        await _provider.DidNotReceive().ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SplitsLargeRowCountsIntoBatches()
    {
        await _sut.GenerateAsync(Model(500, Text("notes")), _provider, "conn");

        Inserts.Count().ShouldBe(3);
    }

    [Fact]
    public async Task Generate_ReportsProgressAfterEachBatch()
    {
        var reported = new List<int>();

        await _sut.GenerateAsync(Model(500, Text("notes")), _provider, "conn", count =>
        {
            reported.Add(count);
            return Task.CompletedTask;
        });

        reported.ShouldBe([200, 400, 500]);
    }

    [Fact]
    public async Task Generate_WhenABatchFails_RollsBackAndReportsTheEnginesMessage()
    {
        var batches = 0;
        _responses["INSERT"] = () => ++batches == 2 ? Failure("duplicate key value violates unique constraint \"orders_pkey\"") : new QueryResult();

        var result = await _sut.GenerateAsync(Model(500, Text("notes")), _provider, "conn");

        result.RowsInserted.ShouldBe(0);
        result.Error.ShouldBe("Inserting rows 201 to 400 failed, so no rows were added: duplicate key value violates unique constraint \"orders_pkey\"");
        Inserts.Count().ShouldBe(2);
        await _provider.Received(1).RollbackTransactionAsync("conn", _transaction.Id);
        await _provider.DidNotReceive().CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WhenTheEngineGivesOnlyRawErrorText_ReportsItWithoutTheDriversPrefix()
    {
        _responses["INSERT"] = () => new QueryResult { Error = "Worker error: SQLITE_CONSTRAINT: sqlite3 result code 19: UNIQUE constraint failed: orders.notes" };

        var result = await _sut.GenerateAsync(Model(10, Text("notes")), _provider, "conn");

        result.Error.ShouldBe("Inserting rows 1 to 10 failed, so no rows were added: UNIQUE constraint failed: orders.notes");
    }

    [Fact]
    public async Task Generate_QuotesNamesAndEscapesTextWithTheEnginesDialect()
    {
        UseDialect(MySqlDialect.Instance, DatabaseType.MySQL);
        var model = Model(1, Text("it's"));
        model.TableName = "order `lines`";
        model.Schema = "";
        Choose(model, "it's", new CustomListGenerator(), "O'Brien\\");

        await _sut.GenerateAsync(model, _provider, "conn");

        Inserts.ShouldHaveSingleItem().ShouldBe("INSERT INTO `order ``lines```\n(`it's`)\nVALUES\n('O''Brien\\\\');");
    }

    [Theory]
    [InlineData(DatabaseType.SQLServer, "bit", "1")]
    [InlineData(DatabaseType.PostgreSQL, "boolean", "TRUE")]
    [InlineData(DatabaseType.MySQL, "tinyint(1)", "TRUE")]
    [InlineData(DatabaseType.WasmSQLite, "INTEGER", "1")]
    public async Task Generate_WritesBooleansTheWayEachEngineAcceptsThem(DatabaseType engine, string type, string literal)
    {
        SqlDialect dialect = engine switch
        {
            DatabaseType.SQLServer => SqlServerDialect.Instance,
            DatabaseType.MySQL => MySqlDialect.Instance,
            DatabaseType.WasmSQLite => SqliteDialect.Instance,
            _ => PostgreSqlDialect.Instance
        };
        UseDialect(dialect, engine);
        var model = Model(1, new ColumnInfo { Name = "is_active", DataType = type });
        Choose(model, "is_active", new CustomListGenerator(), "true");

        await _sut.GenerateAsync(model, _provider, "conn");

        Inserts.ShouldHaveSingleItem().ShouldEndWith($"VALUES\n({literal});");
    }

    [Fact]
    public async Task Generate_LeavesOutColumnsTheDatabaseFillsOrDefaults()
    {
        var model = Model(3,
            new ColumnInfo { Name = "id", DataType = "integer", IsIdentity = true, IsPrimaryKey = true },
            new ColumnInfo { Name = "status", DataType = "text", DefaultValue = "'pending'" },
            Text("notes"));
        Choose(model, "status", new DatabaseDefaultGenerator());

        await _sut.GenerateAsync(model, _provider, "conn");

        Inserts.ShouldHaveSingleItem().ShouldStartWith("INSERT INTO \"public\".\"orders\"\n(\"notes\")\n");
    }

    [Fact]
    public async Task Generate_QualifiesTheTableWithItsSchema()
    {
        UseDialect(SqlServerDialect.Instance, DatabaseType.SQLServer);
        var model = Model(1, Text("notes"));
        model.Schema = "dbo";

        await _sut.GenerateAsync(model, _provider, "conn");

        Inserts.ShouldHaveSingleItem().ShouldStartWith("INSERT INTO [dbo].[orders]");
    }

    [Fact]
    public async Task Generate_FillsForeignKeysWithValuesTheReferencedTableHolds()
    {
        _responses["SELECT *"] = () => Rows("id", 7L, 9L);
        var model = Model(20, new ColumnInfo
        {
            Name = "customer_id",
            DataType = "integer",
            ForeignKey = new ForeignKeyInfo { ColumnName = "customer_id", ReferencedSchema = "public", ReferencedTable = "customers", ReferencedColumn = "id" }
        });

        await _sut.GenerateAsync(model, _provider, "conn");

        _executed[0].ShouldBe("SELECT * FROM \"public\".\"customers\"\nWHERE \"id\" IS NOT NULL\nLIMIT 1000;");
        var values = Inserts.Single().Split("VALUES\n")[1].TrimEnd(';').Split(",\n");
        values.ShouldAllBe(v => v == "(7)" || v == "(9)");
    }

    [Fact]
    public async Task Generate_WhenARequiredForeignKeyHasNothingToReference_SaysWhatToFillFirst()
    {
        _responses["SELECT *"] = () => Rows("id");
        var model = Model(5, new ColumnInfo
        {
            Name = "customer_id",
            DataType = "integer",
            ForeignKey = new ForeignKeyInfo { ColumnName = "customer_id", ReferencedTable = "customers", ReferencedColumn = "id" }
        });

        var result = await _sut.GenerateAsync(model, _provider, "conn");

        result.Error.ShouldBe("\"customer_id\" must match a row in customers, which has none yet. Generate data for customers first.");
        Inserts.ShouldBeEmpty();
        await _provider.Received(1).RollbackTransactionAsync("conn", _transaction.Id);
    }

    [Fact]
    public async Task Generate_NumbersKeysAfterTheHighestExistingOne()
    {
        UseDialect(SqlServerDialect.Instance, DatabaseType.SQLServer);
        _responses["SELECT MAX"] = () => Rows("max_value", 41);
        var model = Model(2, new ColumnInfo { Name = "id", DataType = "int", IsPrimaryKey = true });
        model.Schema = "dbo";

        await _sut.GenerateAsync(model, _provider, "conn");

        _executed[0].ShouldBe("SELECT MAX([id]) AS [max_value] FROM [dbo].[orders];");
        Inserts.ShouldHaveSingleItem().ShouldEndWith("VALUES\n(42),\n(43);");
    }

    [Fact]
    public async Task Generate_WithAStartChosen_NumbersFromThere()
    {
        var model = Model(2, new ColumnInfo { Name = "id", DataType = "int", IsPrimaryKey = true });
        model.ColumnGenerators[0].Options.StartValue = 100;

        await _sut.GenerateAsync(model, _provider, "conn");

        _executed.ShouldNotContain(sql => sql.StartsWith("SELECT MAX"));
        Inserts.ShouldHaveSingleItem().ShouldEndWith("VALUES\n(100),\n(101);");
    }

    [Fact]
    public async Task Generate_WithoutADialect_SaysTheEngineIsNotSupported()
    {
        var liteDb = Substitute.For<IDatabaseProvider>();
        liteDb.DatabaseType.Returns(DatabaseType.LiteDB);

        var result = await _sut.GenerateAsync(Model(5, Text("notes")), liteDb, "conn");

        result.Error.ShouldBe("Generating data is not supported for LiteDB connections.");
        await liteDb.DidNotReceive().BeginTransactionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WhenTheTransactionCannotStart_ReportsWhy()
    {
        _provider.BeginTransactionAsync("conn").ThrowsAsync(new InvalidOperationException("Database 'shop' has an open transaction in a query tab."));

        var result = await _sut.GenerateAsync(Model(5, Text("notes")), _provider, "conn");

        result.Error.ShouldBe("Could not start a transaction: Database 'shop' has an open transaction in a query tab.");
        _executed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Generate_WhenTheCommitFails_ReportsThatNothingWasAdded()
    {
        _provider.CommitTransactionAsync("conn", _transaction.Id).ThrowsAsync(new InvalidOperationException("connection reset"));

        var result = await _sut.GenerateAsync(Model(5, Text("notes")), _provider, "conn");

        result.RowsInserted.ShouldBe(0);
        result.Error.ShouldBe("Generating data failed, so no rows were added: connection reset");
        await _provider.Received(1).RollbackTransactionAsync("conn", _transaction.Id);
    }

    [Fact]
    public async Task Generate_WhenTheModelHasProblems_RunsNothing()
    {
        var model = Model(5, new ColumnInfo { Name = "span", DataType = "tsrange" });

        var result = await _sut.GenerateAsync(model, _provider, "conn");

        result.Error.ShouldBe("Pick a generator for \"span\": it can't be NULL and has no default");
        await _provider.DidNotReceive().BeginTransactionAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WhenCancelledPartWay_RollsBackWhatWasInserted()
    {
        using var cancellation = new CancellationTokenSource();

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.GenerateAsync(
            Model(1000, Text("notes")), _provider, "conn", _ => cancellation.CancelAsync(), cancellation.Token));

        Inserts.Count().ShouldBe(1);
        await _provider.Received(1).RollbackTransactionAsync("conn", _transaction.Id);
        await _provider.DidNotReceive().CommitTransactionAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Generate_WhenAlreadyCancelled_StartsNothing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.GenerateAsync(Model(10, Text("notes")), _provider, "conn", cancellationToken: cancellation.Token));

        await _provider.DidNotReceive().BeginTransactionAsync(Arg.Any<string>());
    }
}
