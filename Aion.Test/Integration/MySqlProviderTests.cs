using Aion.Core.Database;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.MySql;
using Xunit;

namespace Aion.Test.Integration;

public class MySqlProviderTests : DatabaseProviderTestBase, IAsyncLifetime
{
    private readonly MySqlContainer _container;
    
    protected override string TestSchema => "";

    public MySqlProviderTests()
        : base(new MySqlProvider(new Logger<MySqlProvider>(new LoggerFactory())), string.Empty)
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithUsername("root")
            .WithPassword("test_password")
            .WithEnvironment("MYSQL_ROOT_HOST", "%")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "test_password")
            .WithEnvironment("MYSQL_ALLOW_EMPTY_PASSWORD", "yes")
            .WithPortBinding(3306, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306))
            .WithAutoRemove(true)
            .Build();
    }

    public override async Task InitializeAsync()
    {
        try 
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            await Task.Delay(5000);
            await base.InitializeAsync();
        }
        catch (Exception ex)
        {
            try { await _container.DisposeAsync(); } catch { }
            throw new Exception($"Failed to initialize MySQL container: {ex.Message}", ex);
        }
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetDatabases_ShouldReturnDatabases()
    {
        // Act
        var databases = await Provider.GetDatabasesAsync(ConnectionString);

        // Assert
        databases.ShouldNotBeNull();
        databases.ShouldContain(TestDatabase);
    }

    [Fact]
    public async Task GetTables_ShouldReturnTables()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        // Act
        var tables = await Provider.GetTablesAsync(dbConnectionString, TestDatabase);

        // Assert
        tables.ShouldNotBeNull();
        tables.ShouldContain(t => t.Name == TestTable);
    }

    [Fact]
    public async Task GetColumns_ShouldReturnColumns()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        // Act
        var columns = await Provider.GetColumnsAsync(dbConnectionString, TestDatabase, "", TestTable);

        // Assert
        columns.ShouldNotBeNull();
        columns.Count.ShouldBe(3);
        
        var idColumn = columns.First(c => c.Name == "id");
        idColumn.DataType.ShouldBe("int");  // MySQL returns 'int' instead of 'integer'
        idColumn.IsNullable.ShouldBeFalse();
        
        var nameColumn = columns.First(c => c.Name == "name");
        nameColumn.DataType.ShouldBe("varchar");  // MySQL returns 'varchar' without length in type
        nameColumn.IsNullable.ShouldBeFalse();
        nameColumn.MaxLength.ShouldBe(100);
        
        var descColumn = columns.First(c => c.Name == "description");
        descColumn.DataType.ShouldBe("text");
        descColumn.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteQuery_ShouldExecuteInsertAndSelect()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var insertScript = await Provider.Commands.GenerateInsertScript(
            TestDatabase,
            "",
            TestTable,
            new[]
            {
                new ColumnValue("id", 1),
                new ColumnValue("name", "Test"),
                new ColumnValue("description", "Test Description")
            });

        // Act
        var insertResult = await Provider.ExecuteQueryAsync(dbConnectionString, insertScript, CancellationToken.None);
        var selectResult = await Provider.ExecuteQueryAsync(
            dbConnectionString, 
            $"SELECT * FROM {TestTable}", 
            CancellationToken.None);

        // Assert
        ValidateQueryResult(insertResult);
        ValidateQueryResult(selectResult);
        selectResult.Rows.Count.ShouldBe(1);
        selectResult.Rows[0]["name"].ToString().ShouldBe("Test");
    }

    [Fact]
    public async Task GetQueryPlan_ShouldReturnPlan()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var query = $"SELECT * FROM {TestTable}";

        // Act
        var estimatedPlan = await Provider.GetEstimatedPlanAsync(dbConnectionString, query);
        var actualPlan = await Provider.GetActualPlanAsync(dbConnectionString, query);

        // Assert
        estimatedPlan.ShouldNotBeNull();
        estimatedPlan.PlanContent.ShouldNotBeNullOrEmpty();
        
        actualPlan.ShouldNotBeNull();
        actualPlan.PlanContent.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateConnectionString_ShouldValidateCorrectly()
    {
        // Valid connection string
        var isValid = Provider.ValidateConnectionString(ConnectionString, out var error);
        isValid.ShouldBeTrue();
        error.ShouldBeNull();

        // Invalid connection string
        isValid = Provider.ValidateConnectionString("Server=;", out error);
        isValid.ShouldBeFalse();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void GetDefaultPort_ShouldReturnCorrectPort()
    {
        Provider.GetDefaultPort().ShouldBe(3306);
    }

    [Fact]
    public async Task GetIndexes_ShouldReturnCreatedIndex()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var createIndex = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            $"CREATE UNIQUE INDEX ix_{TestTable}_name ON {TestTable} (name)",
            CancellationToken.None);
        createIndex.Error.ShouldBeNull();

        // Act
        var indexProvider = (IDatabaseIndexProvider)Provider;
        var indexes = await indexProvider.GetIndexesAsync(dbConnectionString, TestDatabase);

        // Assert
        indexes.ShouldNotBeNull();
        var created = indexes.FirstOrDefault(i => i.Name == $"ix_{TestTable}_name");
        created.ShouldNotBeNull();
        created!.TableName.ShouldBe(TestTable);
        created.IsUnique.ShouldBeTrue();
        created.IsPrimary.ShouldBeFalse();
        created.Columns.ShouldContain("name");
    }

    [Fact]
    public async Task GetRoutines_ShouldReturnCreatedFunctionAndProcedure()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        var createFn = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            "CREATE FUNCTION aion_test_fn(x INT) RETURNS INT DETERMINISTIC RETURN x + 1;",
            CancellationToken.None);
        createFn.Error.ShouldBeNull();

        var createProc = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            "CREATE PROCEDURE aion_test_proc() BEGIN SELECT 1; END",
            CancellationToken.None);
        createProc.Error.ShouldBeNull();

        // Act
        var routineProvider = (IDatabaseRoutineProvider)Provider;
        var routines = await routineProvider.GetRoutinesAsync(dbConnectionString, TestDatabase);

        // Assert
        routines.ShouldNotBeNull();
        var fn = routines.FirstOrDefault(r => r.Name == "aion_test_fn");
        fn.ShouldNotBeNull();
        fn!.Kind.ShouldBe(RoutineKind.Function);
        fn.ReturnType.ShouldNotBeNullOrEmpty();

        var proc = routines.FirstOrDefault(r => r.Name == "aion_test_proc");
        proc.ShouldNotBeNull();
        proc!.Kind.ShouldBe(RoutineKind.Procedure);
        proc.ReturnType.ShouldBeNull();
    }
}
