using System.Xml.Linq;
using Aion.Components.Connections;
using Aion.Contracts.Database;
using Aion.Core.Database.SqlServer;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace Aion.Test.Integration;

public class SqlServerProviderTests : DatabaseProviderTestBase, IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    
    protected override string TestSchema => "dbo";

    public SqlServerProviderTests()
        : base(new SqlServerProvider(new Logger<SqlServerProvider>(new LoggerFactory())), string.Empty)
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Strong_Password_123!")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_PID", "Developer")
            .WithPortBinding(1433, true)
            .WithAutoRemove(true)
            .Build();
    }

    public override async Task InitializeAsync()
    {
        try 
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            await SqlServerReadiness.WaitForLoginAsync(Provider, ConnectionString);
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

    [Fact(Skip = "Setup error")]
    public async Task GetDatabases_ShouldReturnDatabases()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        // Act
        var databases = await Provider.GetDatabasesAsync(dbConnectionString);

        // Assert
        databases.ShouldNotBeNull();
        databases.ShouldContain(TestDatabase);
    }

    [Fact(Skip = "Setup error")]
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

    [Fact(Skip = "Setup error")]
    public async Task GetColumns_ShouldReturnColumns()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, "master");

        // Act
        var columns = await Provider.GetColumnsAsync(dbConnectionString, TestDatabase, "dbo", TestTable);

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

    [Fact(Skip = "Setup error")]
    public async Task ExecuteQuery_ShouldExecuteInsertAndSelect()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var insertScript = await Provider.Commands.GenerateInsertScript(
            TestDatabase,
            "dbo",
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
        estimatedPlan.PlanFormat.ShouldBe("XML");
        estimatedPlan.PlanContent.ShouldNotStartWith("Error");
        XDocument.Parse(estimatedPlan.PlanContent).Root!.Name.LocalName.ShouldBe("ShowPlanXML");

        actualPlan.PlanFormat.ShouldBe("XML");
        actualPlan.PlanContent.ShouldNotStartWith("Error");
        XDocument.Parse(actualPlan.PlanContent).Root!.Name.LocalName.ShouldBe("ShowPlanXML");
    }

    [Fact]
    public async Task EstimatedPlan_ForUpdate_ShouldNotExecuteIt()
    {
        // Arrange
        await InsertRowAsync(1, "original");

        // Act
        var plan = await Provider.GetEstimatedPlanAsync(DatabaseConnectionString, ActualPlanUpdateStatement);
        var afterPlan = await ReadNameAsync(1);
        var update = await Provider.ExecuteQueryAsync(DatabaseConnectionString, ActualPlanUpdateStatement, CancellationToken.None);

        // Assert
        XDocument.Parse(plan.PlanContent).Root!.Name.LocalName.ShouldBe("ShowPlanXML");
        afterPlan.ShouldBe("original");
        ValidateQueryResult(update);
        (await ReadNameAsync(1)).ShouldBe("changed");
    }

    [Fact]
    public async Task GetColumns_ReportsTypesTheSchemaTreeShortens()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, """
            CREATE TABLE dbo.column_types (
                a_nvarchar nvarchar(100), a_nvarchar_max nvarchar(max), a_varbinary_max varbinary(max), a_char char(3),
                a_text text, a_xml xml, a_datetime2 datetime2, a_guid uniqueidentifier, a_version rowversion)
            """);

        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, "dbo", "column_types");

        columns.Select(c => ColumnTypeText.Short(c, DatabaseType.SQLServer)).ShouldBe(
        [
            "nvarchar(100)", "nvarchar(max)", "varbinary(max)", "char(3)",
            "text", "xml", "datetime2", "uniqueidentifier", "rowversion"
        ]);
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
    public async Task GetDatabases_WrongPassword_ThrowsDriverError()
    {
        var wrongPassword = new SqlConnectionStringBuilder(ConnectionString) { Password = "Definitely_Wrong_123!" }.ConnectionString;

        var ex = await Should.ThrowAsync<SqlException>(() => Provider.GetDatabasesAsync(wrongPassword));

        ex.Message.ShouldContain("Login failed");
    }

    [Fact]
    public async Task GetDatabases_ListsUserDatabases()
    {
        const string database = "aion_listing_db";
        var created = await Provider.ExecuteQueryAsync(ConnectionString, $"CREATE DATABASE [{database}]", CancellationToken.None);
        created.Error.ShouldBeNull();

        var databases = await Provider.GetDatabasesAsync(ConnectionString);

        databases.ShouldNotBeNull();
        databases.ShouldContain(database);
        databases.ShouldNotContain("master");
    }

    [Fact]
    public void GetDefaultPort_ShouldReturnCorrectPort()
    {
        Provider.GetDefaultPort().ShouldBe(1433);
    }

    [Fact(Skip = "Setup error")]
    public async Task GetIndexes_ShouldReturnCreatedIndex()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var createIndex = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            $"CREATE UNIQUE INDEX ix_{TestTable}_name ON [dbo].[{TestTable}] (name)",
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
        created.TableSchema.ShouldBe("dbo");
        created.IsUnique.ShouldBeTrue();
        created.IsPrimary.ShouldBeFalse();
        created.Columns.ShouldContain("name");
    }

    [Fact(Skip = "Setup error")]
    public async Task GetRoutines_ShouldReturnCreatedFunctionAndProcedure()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        var createFn = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            "CREATE FUNCTION dbo.aion_test_fn(@x INT) RETURNS INT AS BEGIN RETURN @x + 1 END",
            CancellationToken.None);
        createFn.Error.ShouldBeNull();

        var createProc = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            "CREATE PROCEDURE dbo.aion_test_proc AS BEGIN SELECT 1 END",
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

        var proc = routines.FirstOrDefault(r => r.Name == "aion_test_proc");
        proc.ShouldNotBeNull();
        proc!.Kind.ShouldBe(RoutineKind.Procedure);
    }

    private const string EditTable = "edit_target";
    private const string EditSelect = "SELECT * FROM [dbo].[edit_target] ORDER BY id";

    // Creates the database itself if needed so these tests don't depend on the shared setup order.
    private async Task<string> CreateEditTableAsync()
    {
        var masterConnectionString = Provider.UpdateConnectionString(ConnectionString, "master");

        // The port opens before the server accepts logins, which the container wait strategy does not cover.
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var probe = await Provider.ExecuteQueryAsync(masterConnectionString, "SELECT 1", CancellationToken.None);
            if (probe.Error == null)
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        await ExecuteOrFailAsync(masterConnectionString, $"IF DB_ID(N'{TestDatabase}') IS NULL CREATE DATABASE [{TestDatabase}]");

        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        await ExecuteOrFailAsync(dbConnectionString,
            $"CREATE TABLE [dbo].[{EditTable}] (id int PRIMARY KEY, name nvarchar(200) NOT NULL)");
        await ExecuteOrFailAsync(dbConnectionString,
            $"INSERT INTO [dbo].[{EditTable}] (id, name) VALUES (0, N'Zero'), (1, N'Ada'), (2, N'Grace')");
        return dbConnectionString;
    }

    private async Task<List<object?>> ReadNamesAsync(string dbConnectionString)
    {
        var result = await ExecuteOrFailAsync(dbConnectionString, $"SELECT name FROM [dbo].[{EditTable}] ORDER BY id");
        return result.Rows.Select(r => (object?)r["name"]).ToList();
    }

    [Fact]
    public async Task GridEdit_ValueWithApostropheAndInjection_ChangesExactlyOneRow()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "dbo", EditTable, EditSelect);
        const string newName = @"O'Brien's Hub \ '; DROP TABLE edit_target; --";

        // Act
        var (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, UpdateCell(editable, 1, "name", newName));

        // Assert
        result.Error.ShouldBeNull();
        result.RowsAffected.ShouldBe(1);
        (await ReadNamesAsync(dbConnectionString)).ShouldBe(new object?[] { "Zero", newName, "Grace" });
    }

    [Fact]
    public async Task GridEdit_Delete_RemovesOnlyTheTargetRow()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "dbo", EditTable, EditSelect);

        // Act
        var (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, DeleteRow(editable, 0));

        // Assert
        result.RowsAffected.ShouldBe(1);
        (await ReadNamesAsync(dbConnectionString)).ShouldBe(new object?[] { "Ada", "Grace" });
    }

    [Fact]
    public async Task GridEdit_RowDeletedSinceLoad_ReportsZeroRowsAffected()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "dbo", EditTable, EditSelect);
        await ExecuteOrFailAsync(dbConnectionString, $"DELETE FROM [dbo].[{EditTable}] WHERE id = 2");

        // Act
        var (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, UpdateCell(editable, 2, "name", "gone"));

        // Assert
        result.Error.ShouldBeNull();
        result.RowsAffected.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteQuery_Select_ReportsNoRowsAffected()
    {
        var dbConnectionString = await CreateEditTableAsync();

        var result = await ExecuteOrFailAsync(dbConnectionString, EditSelect);

        result.RowsAffected.ShouldBeNull();
    }
} 