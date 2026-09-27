using System.Globalization;
using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Querying.Editing;
using Aion.Core.Database;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Contracts.Queries.Editing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
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

    protected override string GeneratedRowsTableSql =>
        "CREATE TABLE generated_rows (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, label varchar(20) NOT NULL, active boolean NOT NULL, note text NULL, code integer UNIQUE)";

    protected override string ComputedTotalsTableSql =>
        "CREATE TABLE computed_totals (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, price numeric(10,2) NOT NULL, quantity integer NOT NULL, created_at timestamp DEFAULT now(), total numeric GENERATED ALWAYS AS (price * quantity) STORED)";

    protected override string DecimalTypeName => "numeric";

    protected override string UnknownColumnCode => "SQLSTATE 42703";

    protected override string[] TestTableResultTypes => ["integer", "varchar(100)", "text"];

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

    /// <summary>
    /// The user's server: tstransit.message_data lives in one database, while the database the connection string
    /// names has a tstransit schema without that table, so SQL run against the wrong database fails with
    /// "relation does not exist".
    /// </summary>
    private async Task<(ConnectionState Connections, QueryState Queries, ConnectionModel Connection)> SchemaTableConnectionAsync()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, """
            CREATE SCHEMA tstransit;
            CREATE TABLE tstransit.message (id bigint PRIMARY KEY, body text);
            CREATE TABLE tstransit.message_data (
                id bigint PRIMARY KEY,
                message_id bigint NOT NULL REFERENCES tstransit.message (id),
                payload text,
                created_at timestamptz NOT NULL DEFAULT now());
            INSERT INTO tstransit.message (id, body) VALUES (1, 'first'), (2, 'second');
            INSERT INTO tstransit.message_data (id, message_id, payload) VALUES (10, 1, 'a'), (11, 2, 'b');
            """);
        await ExecuteOrFailAsync(ConnectionString, "CREATE DATABASE other_db");
        await ExecuteOrFailAsync(Provider.UpdateConnectionString(ConnectionString, "other_db"),
            "CREATE SCHEMA tstransit; CREATE TABLE tstransit.message (id bigint PRIMARY KEY)");

        var factory = new DatabaseProviderFactory([Provider]);
        var connections = new ConnectionState(new TestDoubles.ConnectionServiceFake(factory), factory, Substitute.For<IMessageBus>(),
            NullLogger<ConnectionState>.Instance, new TestDoubles.ConnectionSecretStoreFake());
        var connection = new ConnectionModel
        {
            Name = "local",
            ConnectionString = Provider.UpdateConnectionString(ConnectionString, "other_db"),
            Type = Provider.DatabaseType,
            Active = true,
            Databases = [new DatabaseModel { Name = "other_db" }, new DatabaseModel { Name = TestDatabase }]
        };
        connections.Connections.Add(connection);

        return (connections, new QueryState(Substitute.For<IMessageBus>(), Substitute.For<IQuerySaveService>()), connection);
    }

    [Fact]
    public async Task SelectFirstRows_FromATableInANamedSchema_ReadsItFromTheDatabaseTheTreeListsItUnder()
    {
        // Arrange
        var (connections, queries, connection) = await SchemaTableConnectionAsync();

        // Act
        await new TableRowsOpener(connections, queries, Substitute.For<IMessageBus>())
            .Consume(new OpenTableRows(connection.Id, TestDatabase, "tstransit", "message_data"));
        var tab = queries.Queries.Last();
        var result = await connections.ExecuteQueryAsync(tab, CancellationToken.None);

        // Assert
        tab.Query.ShouldBe("SELECT * FROM \"tstransit\".\"message_data\"\nLIMIT 1000;");
        result.Error.ShouldBeNull();
        result.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EditData_OnATableInANamedSchema_ReadsItAndWritesAnEditBack()
    {
        // Arrange
        var (connections, queries, connection) = await SchemaTableConnectionAsync();

        // Act
        await new TableEditorOpener(connections, queries, Substitute.For<IMessageBus>(), NullLogger<TableEditorOpener>.Instance)
            .Consume(new OpenTableEditor(connection.Id, TestDatabase, "tstransit", "message_data"));
        var tab = queries.Queries.Last();
        var result = await connections.ExecuteQueryAsync(tab, CancellationToken.None);
        var metadata = tab.EditMetadata.ShouldNotBeNull();
        var editable = EditableQueryResult.FromQueryResult(result, metadata.SourceTable, metadata.SourceSchema,
            metadata.SourceDatabase, metadata.ConnectionId, metadata.ColumnMetadata);
        var rowIndex = editable.Rows.FindIndex(r => Convert.ToInt64(r["id"]) == 10);
        var plan = await new PendingChangesSqlBuilder(connections, new SqlChangeGenerator())
            .BuildAsync(editable, [UpdateCell(editable, rowIndex, "payload", "edited")]);
        var statement = plan.Generation.Statements.ShouldHaveSingleItem();
        var update = await plan.Provider.ExecuteQueryAsync(plan.ConnectionString, statement.Sql, CancellationToken.None);

        // Assert
        result.Error.ShouldBeNull();
        metadata.IsEditMode.ShouldBeTrue();
        update.Error.ShouldBeNull(statement.Sql);
        update.RowsAffected.ShouldBe(1);
        var payload = await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT payload FROM tstransit.message_data WHERE id = 10");
        payload.Rows.Single()["payload"].ShouldBe("edited");
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
    public async Task GetTables_EstimatesRowCountsFromTableStatistics()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, """
            CREATE TABLE counted (id int);
            INSERT INTO counted SELECT generate_series(1, 250);
            ANALYZE counted;
            CREATE TABLE never_filled (id int);
            """);

        var tables = await Provider.GetTablesAsync(DatabaseConnectionString, TestDatabase);

        tables.Single(t => t.Name == "counted").RowCount.ShouldBe(TableRowCount.Estimated(250));
        tables.Single(t => t.Name == "never_filled").RowCount.ShouldBe(TableRowCount.Estimated(0));
    }

    [Fact]
    public async Task GetViews_ListsViewsApartFromTablesWithColumnsThatLoadLikeATables()
    {
        await ExecuteOrFailAsync(DatabaseConnectionString, $"CREATE VIEW named_rows AS SELECT id, name FROM {TestTable}");

        var views = await ((IDatabaseViewProvider)Provider).GetViewsAsync(DatabaseConnectionString, TestDatabase);
        var tables = await Provider.GetTablesAsync(DatabaseConnectionString, TestDatabase);
        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, "public", "named_rows");

        views.ShouldContain(new TableInfo("public", "named_rows"));
        tables.ShouldNotContain(t => t.Name == "named_rows");
        columns.Select(c => c.Name).ShouldBe(["id", "name"]);
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
            CREATE TYPE mood AS ENUM ('ok', 'sad');
            CREATE TABLE column_types (
                a_varchar varchar(255), a_char char(3), a_text text, a_timestamp timestamp, a_timestamptz timestamptz,
                a_time time, a_timetz timetz, a_double double precision, a_numeric numeric(10,2), a_varbit varbit(8), a_ints int[],
                a_tags varchar(20)[], a_mood mood, a_moods mood[], a_any_numeric numeric, a_whole_numeric numeric(12), a_prices numeric(8,2)[])
            """);

        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, "public", "column_types");

        columns.Select(c => ColumnTypeText.Short(c, DatabaseType.PostgreSQL)).ShouldBe(
        [
            "varchar(255)", "char(3)", "text", "timestamp", "timestamptz",
            "time", "timetz", "double", "numeric(10,2)", "varbit(8)", "integer[]",
            "varchar[]", "mood", "mood[]", "numeric", "numeric(12,0)", "numeric[]"
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
            $"CREATE TABLE {EditTable} (id integer PRIMARY KEY, name varchar(200) NOT NULL, payload bytea, price numeric(10,2), active boolean, note varchar(200))");
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
    public async Task GridEdit_EmptyTextAndNull_AreStoredDistinctly()
    {
        var dbConnectionString = await CreateEditTableAsync();
        await ExecuteOrFailAsync(dbConnectionString, $"UPDATE {EditTable} SET note = 'kept' WHERE id = 2");
        var editable = await LoadEditableTableAsync(dbConnectionString, "public", EditTable, EditSelect);

        await AssertEmptyTextAndNullStayDistinctAsync(dbConnectionString, editable, 1, EditTable, 2);
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
