using System.Globalization;
using Aion.Components.Connections;
using Aion.Core.Database;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Contracts.Queries.Editing;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Aion.Test.Integration;

public class PostgreSqlProviderTests : DatabaseProviderTestBase, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    
    public PostgreSqlProviderTests() 
        : base(new PostgreSqlProvider(), string.Empty)
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithPassword("postgres")
            .Build();
    }

    protected override string UnknownColumnCode => "SQLSTATE 42703";

    public override async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task FailedStatement_UsesThePositionPostgresReports()
    {
        // Act: the message names no token, so only the reported position can place this error.
        var result = await Provider.ExecuteQueryAsync(DatabaseConnectionString,
            $"SELECT name\nFROM {TestTable}\nWHERE id = 'x'", CancellationToken.None);

        // Assert
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Code.ShouldBe("SQLSTATE 22P02");
        result.ErrorDetail.Token.ShouldBeNull();
        result.ErrorDetail.Line.ShouldBe(3);
        result.ErrorDetail.Column.ShouldBe(12);
        result.ErrorDetail.EndColumn.ShouldBe(15);
    }

    [Fact]
    public async Task FailedStatement_InALaterStatement_IsPlacedInThatStatement()
    {
        // Act
        var result = await Provider.ExecuteQueryAsync(DatabaseConnectionString,
            $"SELECT 1;\nSELECT name FROM {TestTable} WHERE id = 'x'", CancellationToken.None);

        // Assert
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Line.ShouldBe(2);
        result.ErrorDetail.Column.ShouldBe(40);
        result.ErrorDetail.EndColumn.ShouldBe(43);
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
    public async Task GetDatabases_ShouldIncludePostgresDatabase()
    {
        var databases = await Provider.GetDatabasesAsync(ConnectionString);

        databases.ShouldNotBeNull();
        databases.ShouldContain("postgres");
    }

    [Fact]
    public async Task GetDatabases_WithDatabaseInConnectionString_ListsAllDatabases()
    {
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        var databases = await Provider.GetDatabasesAsync(dbConnectionString);

        databases.ShouldNotBeNull();
        databases.ShouldContain(TestDatabase);
        databases.ShouldContain("postgres");
        databases.ShouldNotContain("template0");
        databases.ShouldNotContain("template1");
    }

    [Fact]
    public async Task GetDatabases_WrongPassword_ThrowsDriverError()
    {
        var wrongPassword = new NpgsqlConnectionStringBuilder(ConnectionString) { Password = "definitely-wrong" }.ConnectionString;

        var ex = await Should.ThrowAsync<PostgresException>(() => Provider.GetDatabasesAsync(wrongPassword));

        ex.Message.ShouldContain("password authentication failed");
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
        tables.ShouldContain(t => t.Schema == "public" && t.Name == TestTable);
    }

    [Fact]
    public async Task GetColumns_ShouldReturnColumns()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        // Act
        var columns = await Provider.GetColumnsAsync(dbConnectionString, TestDatabase, "public", TestTable);

        // Assert
        columns.ShouldNotBeNull();
        columns.Count.ShouldBe(3);
        
        var idColumn = columns.First(c => c.Name == "id");
        idColumn.DataType.ShouldBe("integer");
        idColumn.IsNullable.ShouldBeFalse();
        
        var nameColumn = columns.First(c => c.Name == "name");
        nameColumn.DataType.ShouldBe("character varying");
        nameColumn.IsNullable.ShouldBeFalse();
        nameColumn.MaxLength.ShouldBe(100);
        
        var descColumn = columns.First(c => c.Name == "description");
        descColumn.DataType.ShouldBe("text");
        descColumn.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public async Task GetColumns_FlagsSerialAndGeneratedIdentityColumnsAsIdentity()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, """
            CREATE TABLE identities (
                serial_id serial, always_id int GENERATED ALWAYS AS IDENTITY, default_id bigint GENERATED BY DEFAULT AS IDENTITY, plain int)
            """);

        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, "public", "identities");

        columns.Where(c => c.IsIdentity).Select(c => c.Name).ShouldBe(["serial_id", "always_id", "default_id"]);
    }

    [Fact]
    public async Task GetColumns_ReportsTypesTheSchemaTreeShortens()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, """
            CREATE TABLE column_types (
                a_varchar varchar(255), a_char char(3), a_text text, a_timestamp timestamp, a_timestamptz timestamptz,
                a_time time, a_timetz timetz, a_double double precision, a_numeric numeric(10,2), a_varbit varbit(8), a_ints int[])
            """);

        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, "public", "column_types");

        columns.Select(c => ColumnTypeText.Short(c, DatabaseType.PostgreSQL)).ShouldBe(
        [
            "varchar(255)", "char(3)", "text", "timestamp", "timestamptz",
            "time", "timetz", "double", "numeric", "varbit(8)", "array"
        ]);
    }

    [Fact]
    public async Task ExecuteQuery_ShouldExecuteInsertAndSelect()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var insertScript = await Provider.Commands.GenerateInsertScript(
            TestDatabase,
            "public",
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
        isValid = Provider.ValidateConnectionString("Host=;", out error);
        isValid.ShouldBeFalse();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void GetDefaultPort_ShouldReturnCorrectPort()
    {
        Provider.GetDefaultPort().ShouldBe(5432);
    }

    [Fact]
    public async Task GetIndexes_ShouldReturnCreatedIndex()
    {
        // Arrange
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        var createIndex = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            $"CREATE UNIQUE INDEX IF NOT EXISTS ix_{TestTable}_name ON {TestTable} (name)",
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
        created.TableSchema.ShouldBe("public");
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
            "CREATE OR REPLACE FUNCTION aion_test_fn(x integer) RETURNS integer AS $$ BEGIN RETURN x + 1; END; $$ LANGUAGE plpgsql;",
            CancellationToken.None);
        createFn.Error.ShouldBeNull();

        var createProc = await Provider.ExecuteQueryAsync(
            dbConnectionString,
            "CREATE OR REPLACE PROCEDURE aion_test_proc() AS $$ BEGIN PERFORM 1; END; $$ LANGUAGE plpgsql;",
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
        fn.ArgumentSignature!.ShouldContain("integer");

        var proc = routines.FirstOrDefault(r => r.Name == "aion_test_proc");
        proc.ShouldNotBeNull();
        proc!.Kind.ShouldBe(RoutineKind.Procedure);
        proc.ReturnType.ShouldBeNull();
    }

    private const string EditTable = "edit_target";
    private const string EditSelect = "SELECT * FROM \"public\".\"edit_target\" ORDER BY id";

    private async Task<string> CreateEditTableAsync()
    {
        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);
        await ExecuteOrFailAsync(dbConnectionString,
            $"CREATE TABLE {EditTable} (id integer PRIMARY KEY, name varchar(200) NOT NULL, payload bytea, price numeric(10,2), active boolean)");
        await ExecuteOrFailAsync(dbConnectionString,
            $"INSERT INTO {EditTable} (id, name) VALUES (1, 'Ada'), (2, 'Grace'), (3, 'Linus')");
        return dbConnectionString;
    }

    private async Task<List<object?>> ReadNamesAsync(string dbConnectionString)
    {
        var result = await ExecuteOrFailAsync(dbConnectionString, $"SELECT name FROM {EditTable} ORDER BY id");
        return result.Rows.Select(r => (object?)r["name"]).ToList();
    }

    [Fact]
    public async Task GridEdit_ValueWithApostropheAndInjection_ChangesExactlyOneRow()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "public", EditTable, EditSelect);
        const string newName = @"O'Brien's Hub \ '; DROP TABLE edit_target; --";

        // Act
        var (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, UpdateCell(editable, 1, "name", newName));

        // Assert
        result.Error.ShouldBeNull();
        result.RowsAffected.ShouldBe(1);
        (await ReadNamesAsync(dbConnectionString)).ShouldBe(new object?[] { "Ada", newName, "Linus" });
    }

    [Fact]
    public async Task GridEdit_Delete_RemovesOnlyTheTargetRow()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "public", EditTable, EditSelect);

        // Act
        var (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, DeleteRow(editable, 0));

        // Assert
        result.RowsAffected.ShouldBe(1);
        (await ReadNamesAsync(dbConnectionString)).ShouldBe(new object?[] { "Grace", "Linus" });
    }

    [Fact]
    public async Task GridEdit_RowDeletedSinceLoad_ReportsZeroRowsAffected()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "public", EditTable, EditSelect);
        await ExecuteOrFailAsync(dbConnectionString, $"DELETE FROM {EditTable} WHERE id = 3");

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

    [Fact]
    public async Task ExecuteInTransaction_ReportsRowsAffected()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var transaction = await Provider.BeginTransactionAsync(dbConnectionString);

        // Act
        var result = await Provider.ExecuteInTransactionAsync(
            dbConnectionString, $"UPDATE {EditTable} SET name = name || '!' WHERE id > 1", transaction.Id, CancellationToken.None);
        await Provider.RollbackTransactionAsync(dbConnectionString, transaction.Id);

        // Assert
        result.Error.ShouldBeNull();
        result.RowsAffected.ShouldBe(2);
        (await ReadNamesAsync(dbConnectionString)).ShouldBe(new object?[] { "Ada", "Grace", "Linus" });
    }

    [Fact]
    public async Task GridEdit_TypedValuesRoundTripUnderAnotherCulture()
    {
        // Arrange
        var dbConnectionString = await CreateEditTableAsync();
        var editable = await LoadEditableTableAsync(dbConnectionString, "public", EditTable, EditSelect);
        var original = editable.Rows[0].ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        var change = PendingChange.CreateUpdate(0, original, new Dictionary<string, object?>(original)
        {
            ["price"] = 1234.5m,
            ["active"] = true,
            ["payload"] = new byte[] { 0x00, 0x5C, 0x27 }
        });

        // Act
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        QueryResult result;
        try
        {
            (_, result) = await ApplyGridChangeAsync(dbConnectionString, editable, change);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        // Assert
        result.Error.ShouldBeNull();
        result.RowsAffected.ShouldBe(1);
        var row = (await ExecuteOrFailAsync(dbConnectionString, $"SELECT price, active, payload FROM {EditTable} WHERE id = 1")).Rows.Single();
        row["price"].ShouldBe(1234.5m);
        row["active"].ShouldBe(true);
        row["payload"].ShouldBe(new byte[] { 0x00, 0x5C, 0x27 });
    }

    [Fact]
    public async Task Transaction_CommitAfterFailedStatement_ShouldRefuseAndKeepTransactionForRollback()
    {
        // Arrange
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);
        await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"INSERT INTO {TestTable} (id, name) VALUES (1, 'pending')", transaction.Id, CancellationToken.None);
        var failed = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            "SELECT * FROM table_that_does_not_exist", transaction.Id, CancellationToken.None);

        // Act
        var commit = await Should.ThrowAsync<InvalidOperationException>(() =>
            Provider.CommitTransactionAsync(DatabaseConnectionString, transaction.Id));
        await Provider.RollbackTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Assert
        failed.Error.ShouldNotBeNull();
        commit.Message.ShouldContain("aborted");
        (await CountRowsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task EstimatedPlan_WithMultipleStatements_ShouldNotRunLaterStatements()
    {
        // Arrange
        await InsertRowAsync(1, "original");
        var plans = (IEstimatedQueryPlanProvider)Provider;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => plans.GetEstimatedPlanAsync(
            DatabaseConnectionString, $"SELECT 1; DELETE FROM {TestTable}", CancellationToken.None));
        (await CountRowsAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ActualPlan_ForUpdate_ShouldReportActualTimings()
    {
        // Arrange
        await InsertRowAsync(1, "original");
        var plans = (IActualQueryPlanProvider)Provider;

        // Act
        var plan = await plans.GetActualPlanAsync(DatabaseConnectionString, ActualPlanUpdateStatement, CancellationToken.None);

        // Assert
        plan.PlanContent.ShouldContain("Update on");
        plan.PlanContent.ShouldContain("actual time");
    }
}
