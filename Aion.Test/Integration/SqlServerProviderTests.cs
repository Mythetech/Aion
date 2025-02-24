using Aion.Core.Database;
using Aion.Core.Database.SqlServer;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace Aion.Test.Integration;

public class SqlServerProviderTests : DatabaseProviderTestBase, IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    
    public SqlServerProviderTests() 
        : base(new SqlServerProvider(new Logger<SqlServerProvider>(new LoggerFactory())), string.Empty)
    {
        _container = new MsSqlBuilder()
            /*
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Strong_Password_123!")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithPortBinding(1433, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilPortIsAvailable(1433)
                    .UntilCommandIsCompleted("/opt/mssql-tools/bin/sqlcmd", "-Q", "SELECT 1")
            )
            .WithAutoRemove(true)
            */
            .Build();
    }

    public override async Task InitializeAsync()
    {
        try 
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            await base.InitializeAsync();
        }
        catch (Exception ex)
        {
            try { await _container.DisposeAsync(); } catch { }
            throw new Exception($"Failed to initialize SQL Server container: {ex.Message}", ex);
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
        tables.ShouldContain(TestTable);
    }

    [Fact]
    public async Task GetColumns_ShouldReturnColumns()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        // Act
        var columns = await Provider.GetColumnsAsync(dbConnectionString, TestDatabase, TestTable);

        // Assert
        columns.ShouldNotBeNull();
        columns.Count.ShouldBe(3);
        
        var idColumn = columns.First(c => c.Name == "id");
        idColumn.DataType.ShouldBe("int");
        idColumn.IsNullable.ShouldBeFalse();
        
        var nameColumn = columns.First(c => c.Name == "name");
        nameColumn.DataType.ShouldBe("varchar");
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
            $"SELECT * FROM [{TestTable}]", 
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
        var query = $"SELECT * FROM [{TestTable}]";

        // Act
        var estimatedPlan = await Provider.GetEstimatedPlanAsync(dbConnectionString, query);
        var actualPlan = await Provider.GetActualPlanAsync(dbConnectionString, query);

        // Assert
        estimatedPlan.ShouldNotBeNull();
        estimatedPlan.PlanContent.ShouldNotBeNullOrEmpty();
        estimatedPlan.PlanFormat.ShouldBe("XML");
        
        actualPlan.ShouldNotBeNull();
        actualPlan.PlanContent.ShouldNotBeNullOrEmpty();
        actualPlan.PlanFormat.ShouldBe("XML");
    }

    [Fact]
    public void ValidateConnectionString_ShouldValidateCorrectly()
    {
        // Valid connection string
        var isValid = Provider.ValidateConnectionString(ConnectionString, out var error);
        isValid.ShouldBeTrue();
        error.ShouldBeNull();

        // Invalid connection string
        isValid = Provider.ValidateConnectionString("Data Source=;", out error);
        isValid.ShouldBeFalse();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void GetDefaultPort_ShouldReturnCorrectPort()
    {
        Provider.GetDefaultPort().ShouldBe(1433);
    }
} 