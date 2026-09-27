using Aion.Components.Connections;
using Aion.Components.ForeignKeys;
using Aion.Components.Querying.Editing;
using Aion.Components.RequestContextPanel;
using Aion.Components.Scaffolding.DataGeneration;
using Aion.Core.Database;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Contracts.Queries.Editing;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Aion.Test.Integration;

public abstract class DatabaseProviderTestBase : IAsyncLifetime
{
    protected readonly IDatabaseProvider Provider;
    protected string ConnectionString;
    protected const string TestDatabase = "aion_test_db";
    protected const string TestTable = "test_table";
    protected virtual string TestSchema => "public";

    protected DatabaseProviderTestBase(IDatabaseProvider provider, string connectionString)
    {
        Provider = provider;
        ConnectionString = connectionString;
    }

    public virtual async Task InitializeAsync()
    {
        var createDbScript = await Provider.Commands.GenerateCreateDatabaseScript(TestDatabase);
        await Provider.ExecuteQueryAsync(ConnectionString, createDbScript, CancellationToken.None);

        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        var createTableScript = await Provider.Commands.GenerateCreateTableScript(
            TestDatabase,
            TestSchema,
            TestTable,
            new[]
            {
                new ColumnDefinition("id", "integer", false, "1"),
                new ColumnDefinition("name", "varchar(100)", false),
                new ColumnDefinition("description", "text", true)
            });
        await Provider.ExecuteQueryAsync(dbConnectionString, createTableScript, CancellationToken.None);
    }

    public virtual async Task DisposeAsync()
    {
        var dropDbScript = await Provider.Commands.GenerateDropDatabaseScript(TestDatabase);
        await Provider.ExecuteQueryAsync(ConnectionString, dropDbScript, CancellationToken.None);
    }

    protected static void ValidateQueryResult(QueryResult result)
    {
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
    }

    protected async Task<QueryResult> ExecuteOrFailAsync(string connectionString, string sql)
    {
        var result = await Provider.ExecuteQueryAsync(connectionString, sql, CancellationToken.None);
        result.Error.ShouldBeNull($"Executing {sql}");
        return result;
    }

    /// <summary>
    /// Reads a table the way edit mode does: the rows from a SELECT plus the provider's column metadata.
    /// </summary>
    protected async Task<EditableQueryResult> LoadEditableTableAsync(string connectionString, string schema, string table, string selectSql)
    {
        var rows = await ExecuteOrFailAsync(connectionString, selectSql);
        var columns = await Provider.GetColumnsAsync(connectionString, TestDatabase, schema, table);
        return EditableQueryResult.FromQueryResult(rows, table, schema, TestDatabase, null, columns);
    }

    /// <summary>
    /// Generates the statement edit mode would run for one pending change and executes it.
    /// </summary>
    protected async Task<(string Sql, QueryResult Result)> ApplyGridChangeAsync(string connectionString, EditableQueryResult editable, PendingChange change)
    {
        var generation = await new SqlChangeGenerator().GenerateSqlAsync(editable, [change], Provider.Commands);
        generation.ValidationError.ShouldBeNull();

        var sql = generation.Statements.ShouldHaveSingleItem().Sql;
        var result = await Provider.ExecuteQueryAsync(connectionString, sql, CancellationToken.None);
        return (sql, result);
    }

    protected static PendingChange UpdateCell(EditableQueryResult editable, int rowIndex, string column, object? newValue)
    {
        var original = editable.Rows[rowIndex].ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        var updated = new Dictionary<string, object?>(original) { [column] = newValue };
        return PendingChange.CreateUpdate(rowIndex, original, updated);
    }

    protected static PendingChange DeleteRow(EditableQueryResult editable, int rowIndex)
    {
        var original = editable.Rows[rowIndex].ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        return PendingChange.CreateDelete(rowIndex, original);
    }

    /// <summary>
    /// Clears a required text column to empty text and sets a nullable one to NULL in one grid change, then checks
    /// the row stored exactly that: the engine kept the empty text as text and the NULL as NULL.
    /// </summary>
    protected async Task AssertEmptyTextAndNullStayDistinctAsync(
        string connectionString, EditableQueryResult editable, int rowIndex, string qualifiedTable, object id)
    {
        var original = editable.Rows[rowIndex].ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        original["note"].ShouldNotBeNull();
        var change = PendingChange.CreateUpdate(rowIndex, original,
            new Dictionary<string, object?>(original) { ["name"] = "", ["note"] = null });

        var (sql, result) = await ApplyGridChangeAsync(connectionString, editable, change);

        result.Error.ShouldBeNull(sql);
        result.RowsAffected.ShouldBe(1);
        var row = (await ExecuteOrFailAsync(connectionString,
                $"SELECT name, CASE WHEN name IS NULL THEN 1 ELSE 0 END AS name_is_null, " +
                $"CASE WHEN note IS NULL THEN 1 ELSE 0 END AS note_is_null FROM {qualifiedTable} WHERE id = {id}"))
            .Rows.Single();
        row["name"].ShouldBe("");
        Convert.ToInt32(row["name_is_null"]).ShouldBe(0);
        Convert.ToInt32(row["note_is_null"]).ShouldBe(1);
    }

    protected static string ActualPlanUpdateStatement => $"UPDATE {TestTable} SET name = 'changed' WHERE id = 1";

    protected string DatabaseConnectionString => Provider.UpdateConnectionString(ConnectionString, TestDatabase);

    protected async Task InsertRowAsync(int id, string name)
    {
        var result = await Provider.ExecuteQueryAsync(
            DatabaseConnectionString, $"INSERT INTO {TestTable} (id, name) VALUES ({id}, '{name}')", CancellationToken.None);
        ValidateQueryResult(result);
    }

    protected async Task<long> CountRowsAsync()
    {
        var result = await Provider.ExecuteQueryAsync(
            DatabaseConnectionString, $"SELECT COUNT(*) AS row_count FROM {TestTable}", CancellationToken.None);
        ValidateQueryResult(result);
        return Convert.ToInt64(result.Rows[0]["row_count"]);
    }

    protected async Task<string?> ReadNameAsync(int id)
    {
        var result = await Provider.ExecuteQueryAsync(
            DatabaseConnectionString, $"SELECT name FROM {TestTable} WHERE id = {id}", CancellationToken.None);
        ValidateQueryResult(result);
        return result.Rows.Single()["name"]?.ToString();
    }

    /// <summary>The engine's code for an unknown column, as the provider formats it.</summary>
    protected abstract string UnknownColumnCode { get; }

    /// <summary>
    /// CREATE TABLE generated_rows in the engine's own DDL: an identity key the engine numbers, a required
    /// label up to 20 characters, a required flag in the engine's boolean type, a nullable note and a unique code.
    /// </summary>
    protected abstract string GeneratedRowsTableSql { get; }

    private async Task<DataGenerationModel> GenerationModelAsync(string table, int rows)
    {
        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, TestSchema, table);
        return new DataGenerationModel
        {
            TableName = table,
            Schema = TestSchema,
            Database = TestDatabase,
            RowCount = rows,
            ColumnGenerators = DataGenerationPlan.CreateBindings(columns, Provider.DatabaseType)
        };
    }

    private static ColumnGeneratorBinding Column(DataGenerationModel model, string name) =>
        model.ColumnGenerators.Single(b => string.Equals(b.Column.Name, name, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task GenerateData_WritesQuotedTextFlagsAndNullsAndLeavesTheIdentityToTheEngine()
    {
        // Arrange
        string[] labels = ["O'Brien", "it's", @"back\slash"];
        await ExecuteOrFailAsync(DatabaseConnectionString, GeneratedRowsTableSql);
        var model = await GenerationModelAsync("generated_rows", 250);
        Column(model, "id").FilledByDatabase.ShouldBe("identity");
        Column(model, "active").Generator.ShouldBeOfType<BooleanGenerator>();
        Column(model, "label").Generator = new CustomListGenerator();
        Column(model, "label").Options.CustomValues = string.Join(", ", labels);
        DataGenerationPlan.CompatibleGenerators(Column(model, "note")).ShouldContain(g => g is NullGenerator);
        Column(model, "note").Generator = new NullGenerator();
        Column(model, "code").Generator = new AutoIncrementGenerator();

        // Act
        var result = await new DataGenerationService().GenerateAsync(model, Provider, DatabaseConnectionString);

        // Assert
        result.Error.ShouldBeNull();
        result.RowsInserted.ShouldBe(250);
        var rows = (await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT id, label, active, note, code FROM generated_rows ORDER BY id")).Rows;
        rows.Count.ShouldBe(250);
        rows.Select(r => Convert.ToInt64(r["id"])).ShouldBe(Enumerable.Range(1, 250).Select(i => (long)i));
        rows.Select(r => (string)r["label"]).Distinct().ShouldBe(labels, ignoreOrder: true);
        rows.Select(r => Convert.ToBoolean(r["active"])).Distinct().Count().ShouldBe(2);
        rows.ShouldAllBe(r => r["note"] == null || r["note"] is DBNull);
        rows.Select(r => Convert.ToInt64(r["code"])).ShouldBe(Enumerable.Range(1, 250).Select(i => (long)i));
    }

    [Fact]
    public async Task GenerateData_WhenALaterBatchIsRejected_AddsNoRows()
    {
        // Arrange: code 250 is taken, so only the second batch of 200 collides.
        await ExecuteOrFailAsync(DatabaseConnectionString, GeneratedRowsTableSql);
        var model = await GenerationModelAsync("generated_rows", 250);
        Column(model, "label").Generator = new NameGenerator();
        Column(model, "code").Generator = new AutoIncrementGenerator();
        Column(model, "code").Options.StartValue = 1;
        var seed = await new DataGenerationService().GenerateAsync(await GenerationModelWithCodeAsync(250), Provider, DatabaseConnectionString);
        seed.Error.ShouldBeNull();

        // Act
        var result = await new DataGenerationService().GenerateAsync(model, Provider, DatabaseConnectionString);

        // Assert
        result.RowsInserted.ShouldBe(0);
        result.Error.ShouldNotBeNull().ShouldStartWith("Inserting rows 201 to 250 failed, so no rows were added: ");
        var count = await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT COUNT(*) AS row_count FROM generated_rows");
        Convert.ToInt64(count.Rows[0]["row_count"]).ShouldBe(1);
    }

    private async Task<DataGenerationModel> GenerationModelWithCodeAsync(int code)
    {
        var model = await GenerationModelAsync("generated_rows", 1);
        Column(model, "code").Generator = new AutoIncrementGenerator();
        Column(model, "code").Options.StartValue = code;
        return model;
    }

    [Fact]
    public async Task GenerateData_PointsForeignKeysAtRowsTheReferencedTableHolds()
    {
        // Arrange
        await ExecuteOrFailAsync(DatabaseConnectionString, "CREATE TABLE gen_parent (id int NOT NULL PRIMARY KEY, name varchar(20))");
        await ExecuteOrFailAsync(DatabaseConnectionString, "INSERT INTO gen_parent (id, name) VALUES (1, 'a'), (2, 'b'), (5, 'c')");
        await ExecuteOrFailAsync(DatabaseConnectionString,
            "CREATE TABLE gen_child (id int NOT NULL PRIMARY KEY, parent_id int NOT NULL, FOREIGN KEY (parent_id) REFERENCES gen_parent (id))");
        var model = await GenerationModelAsync("gen_child", 50);
        Column(model, "parent_id").Generator.ShouldBeOfType<ReferencedValueGenerator>();
        Column(model, "id").Generator.ShouldBeOfType<AutoIncrementGenerator>();

        // Act
        var result = await new DataGenerationService().GenerateAsync(model, Provider, DatabaseConnectionString);

        // Assert
        result.Error.ShouldBeNull();
        var rows = (await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT id, parent_id FROM gen_child ORDER BY id")).Rows;
        rows.Select(r => Convert.ToInt64(r["id"])).ShouldBe(Enumerable.Range(1, 50).Select(i => (long)i));
        rows.ShouldAllBe(r => new long[] { 1, 2, 5 }.Contains(Convert.ToInt64(r["parent_id"])));
    }

    /// <summary>
    /// CREATE TABLE computed_totals in the engine's own DDL: an identity key, a required price of the engine's decimal
    /// type with precision 10 and scale 2, a required whole-number quantity, a created_at the engine defaults to the
    /// current time, and total, a column the engine computes as price * quantity.
    /// </summary>
    protected abstract string ComputedTotalsTableSql { get; }

    /// <summary>What the engine calls its exact decimal type: numeric on PostgreSQL, decimal elsewhere.</summary>
    protected abstract string DecimalTypeName { get; }

    [Fact]
    public async Task GetColumns_FlagsOnlyComputedColumnsAsGenerated()
    {
        // Arrange
        await ExecuteOrFailAsync(DatabaseConnectionString, ComputedTotalsTableSql);

        // Act
        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, TestSchema, "computed_totals");

        // Assert: the identity key and the defaulted created_at are filled in by the engine too, but they take values.
        columns.Select(c => c.Name).ShouldBe(["id", "price", "quantity", "created_at", "total"]);
        columns.Where(c => c.IsGenerated).Select(c => c.Name).ShouldBe(["total"]);
        columns.Single(c => c.Name == "id").IsIdentity.ShouldBeTrue();
    }

    [Fact]
    public async Task GetColumns_ReportsTheDeclaredPrecisionAndScale()
    {
        // Arrange
        await ExecuteOrFailAsync(DatabaseConnectionString, ComputedTotalsTableSql);

        // Act
        var columns = await Provider.GetColumnsAsync(DatabaseConnectionString, TestDatabase, TestSchema, "computed_totals");

        // Assert
        var price = columns.Single(c => c.Name == "price");
        ColumnTypeText.Short(price, Provider.DatabaseType).ShouldBe($"{DecimalTypeName}(10,2)");
        ColumnTypeText.Describe(price, Provider.DatabaseType).ShouldStartWith($"{DecimalTypeName}(10,2) · NOT NULL");
        ColumnTypeText.Describe(columns.Single(c => c.Name == "total"), Provider.DatabaseType).ShouldContain(" · generated");
    }

    [Fact]
    public async Task GenerateData_LeavesGeneratedColumnsToTheEngine()
    {
        // Arrange
        await ExecuteOrFailAsync(DatabaseConnectionString, ComputedTotalsTableSql);
        var model = await GenerationModelAsync("computed_totals", 25);
        Column(model, "total").FilledByDatabase.ShouldBe("generated");
        Column(model, "price").Generator.ShouldBeOfType<RandomNumberGenerator>();
        Column(model, "quantity").Generator.ShouldBeOfType<RandomIntGenerator>();

        // Act
        var result = await new DataGenerationService().GenerateAsync(model, Provider, DatabaseConnectionString);

        // Assert
        result.Error.ShouldBeNull();
        var rows = (await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT price, quantity, total FROM computed_totals")).Rows;
        rows.Count.ShouldBe(25);
        rows.ShouldAllBe(r => Convert.ToDecimal(r["total"]) == Convert.ToDecimal(r["price"]) * Convert.ToDecimal(r["quantity"]));
    }

    [Fact]
    public async Task GridEdit_LeavesGeneratedColumnsOutOfInsertsAndUpdates()
    {
        // Arrange
        await ExecuteOrFailAsync(DatabaseConnectionString, ComputedTotalsTableSql);
        await ExecuteOrFailAsync(DatabaseConnectionString, "INSERT INTO computed_totals (price, quantity) VALUES (2.50, 4)");
        var editable = await LoadEditableTableAsync(DatabaseConnectionString, TestSchema, "computed_totals",
            "SELECT id, price, quantity, created_at, total FROM computed_totals ORDER BY id");
        var original = editable.Rows[0].ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        var update = PendingChange.CreateUpdate(0, original,
            new Dictionary<string, object?>(original) { ["quantity"] = "6", ["total"] = "1" });
        var insert = PendingChange.CreateInsert(1, new Dictionary<string, object?>
        {
            ["id"] = null, ["price"] = "1.25", ["quantity"] = "8", ["created_at"] = null, ["total"] = null
        });

        // Act
        var (updateSql, updated) = await ApplyGridChangeAsync(DatabaseConnectionString, editable, update);
        var (insertSql, inserted) = await ApplyGridChangeAsync(DatabaseConnectionString, editable, insert);

        // Assert
        updated.Error.ShouldBeNull(updateSql);
        inserted.Error.ShouldBeNull(insertSql);
        var totals = (await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT total FROM computed_totals ORDER BY id")).Rows
            .Select(r => Convert.ToDecimal(r["total"]));
        totals.ShouldBe([15.00m, 10.00m]);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task GridEdit_WritesTheTextOfABooleanIntoTheEnginesBooleanColumn(string text, bool expected)
    {
        // Arrange: the grid edits every value as text, so a flag arrives as the word it displays.
        await ExecuteOrFailAsync(DatabaseConnectionString, GeneratedRowsTableSql);
        var dialect = ((ISqlDialectProvider)Provider).Dialect;
        await ExecuteOrFailAsync(DatabaseConnectionString,
            $"INSERT INTO generated_rows (label, active) VALUES ('a', {dialect.FormatLiteral(!expected)})");
        var editable = await LoadEditableTableAsync(DatabaseConnectionString, TestSchema, "generated_rows",
            "SELECT id, label, active FROM generated_rows");
        var rules = EditableColumn.For("active", editable.ColumnMetadata, Provider.DatabaseType);
        var committed = CellEditText.Resolve(text, editable.Rows[0]["active"], rules: rules);

        // Act
        var (sql, result) = await ApplyGridChangeAsync(DatabaseConnectionString, editable, UpdateCell(editable, 0, "active", committed));

        // Assert
        result.Error.ShouldBeNull(sql);
        var row = (await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT active FROM generated_rows")).Rows.Single();
        Convert.ToBoolean(row["active"]).ShouldBe(expected);
    }

    /// <summary>The short types the results grid shows for the test table's id, name and description columns.</summary>
    protected abstract string[] TestTableResultTypes { get; }

    [Fact]
    public async Task Select_ReportsEachColumnsType()
    {
        // Arrange
        await InsertRowAsync(1, "a");

        // Act
        var result = await ExecuteOrFailAsync(DatabaseConnectionString, $"SELECT id, name, description FROM {TestTable}");

        // Assert
        result.ColumnTypes.Select(type => ColumnTypeText.Short(type, Provider.DatabaseType)).ShouldBe(TestTableResultTypes);
    }

    [Fact]
    public async Task Select_WithRepeatedColumnNames_KeepsEachColumnsValue()
    {
        // Act
        var result = await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT 1 AS id, 2 AS id, 3 AS id");

        // Assert
        result.ColumnNames.ShouldBe(["id", "id", "id"]);
        result.Columns.ShouldBe(["id", "id_2", "id_3"]);
        var row = result.Rows.ShouldHaveSingleItem();
        Convert.ToInt32(row["id"]).ShouldBe(1);
        Convert.ToInt32(row["id_2"]).ShouldBe(2);
        Convert.ToInt32(row["id_3"]).ShouldBe(3);
    }

    [Fact]
    public async Task Select_WithoutRows_StillReportsEachColumnsType()
    {
        // Act
        var result = await ExecuteOrFailAsync(DatabaseConnectionString, $"SELECT id, name, description FROM {TestTable}");

        // Assert
        result.Rows.ShouldBeEmpty();
        result.ColumnTypes.Select(type => ColumnTypeText.Short(type, Provider.DatabaseType)).ShouldBe(TestTableResultTypes);
    }

    [Fact]
    public async Task FirstRowsSelect_RunsOnTheEngineAndStopsAtTheLimit()
    {
        // Arrange
        await InsertRowAsync(1, "a");
        await InsertRowAsync(2, "b");
        await InsertRowAsync(3, "c");
        var sql = await Provider.Commands.GenerateSelectTopScript(TestDatabase, TestSchema, TestTable, 2);

        // Act
        var result = await ExecuteOrFailAsync(DatabaseConnectionString, sql);

        // Assert
        result.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateTableTemplate_RunsAsWrittenAndNumbersNewRows()
    {
        // Arrange
        var dialect = ((ISqlDialectProvider)Provider).Dialect;

        // Act
        await ExecuteOrFailAsync(DatabaseConnectionString, dialect.CreateTableTemplate());
        await ExecuteOrFailAsync(DatabaseConnectionString, "INSERT INTO new_table (name) VALUES ('first')");
        var rows = await ExecuteOrFailAsync(DatabaseConnectionString, "SELECT id, name, created_at FROM new_table");

        // Assert
        (await Provider.GetTablesAsync(DatabaseConnectionString, TestDatabase)).ShouldContain(t => t.Name == "new_table");
        var row = rows.Rows.ShouldHaveSingleItem();
        Convert.ToInt64(row["id"]).ShouldBe(1);
        row["created_at"].ShouldNotBeNull();
    }

    [Fact]
    public async Task ForeignKeyLookup_FindsTheReferencedRowFromTheProvidersOwnForeignKeyMetadata()
    {
        // Arrange
        const string code = @"O'Brien\";
        await ExecuteOrFailAsync(DatabaseConnectionString,
            "CREATE TABLE fk_parent (code varchar(20) NOT NULL PRIMARY KEY, label varchar(50))");
        await ExecuteOrFailAsync(DatabaseConnectionString,
            "CREATE TABLE fk_child (id int NOT NULL PRIMARY KEY, parent_code varchar(20), FOREIGN KEY (parent_code) REFERENCES fk_parent (code))");
        var dialect = ((ISqlDialectProvider)Provider).Dialect;
        await ExecuteOrFailAsync(DatabaseConnectionString,
            $"INSERT INTO fk_parent (code, label) VALUES ({dialect.FormatLiteral(code)}, 'found'), ('other', 'wrong')");
        var foreignKey = (await Provider.GetForeignKeysAsync(DatabaseConnectionString, TestDatabase, TestSchema, "fk_child"))
            .ShouldHaveSingleItem();

        var factory = new DatabaseProviderFactory([Provider]);
        var connections = new ConnectionState(new TestDoubles.ConnectionServiceFake(factory), factory,
            NSubstitute.Substitute.For<Mythetech.Framework.Infrastructure.MessageBus.IMessageBus>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectionState>.Instance);
        var connection = new Aion.Contracts.Connections.ConnectionModel
        {
            Name = "integration", ConnectionString = ConnectionString, Type = Provider.DatabaseType, Active = true
        };
        connections.Connections.Add(connection);
        var detail = new ForeignKeyDetail("fk_child", foreignKey.ColumnName, foreignKey.ReferencedTable,
            foreignKey.ReferencedColumn, code, connection.Id, TestDatabase, foreignKey.ReferencedSchema);

        // Act
        var lookup = await new ForeignKeyService(connections).FetchReferencedRowAsync(detail);

        // Assert
        lookup.Error.ShouldBeNull();
        lookup.Row.ShouldNotBeNull()["label"].ShouldBe("found");
    }

    [Fact]
    public async Task FailedStatement_ReportsTheUnknownColumnAndTheEngineCode()
    {
        // Act
        var result = await Provider.ExecuteQueryAsync(
            DatabaseConnectionString, $"SELECT categry_id FROM {TestTable}", CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Raw.ShouldBe(result.Error);
        result.ErrorDetail.Kind.ShouldBe(QueryErrorKind.UnknownColumn);
        result.ErrorDetail.Token.ShouldBe("categry_id");
        result.ErrorDetail.Title.ShouldBe("No such column");
        result.ErrorDetail.Code.ShouldBe(UnknownColumnCode);
    }

    [Fact]
    public async Task FailedStatement_ReportsTheLineAndColumnOfTheUnknownColumn()
    {
        // Act
        var result = await Provider.ExecuteQueryAsync(
            DatabaseConnectionString, $"SELECT id,\n  categry_id\nFROM {TestTable}", CancellationToken.None);

        // Assert
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Line.ShouldBe(2);
        result.ErrorDetail.Column.ShouldBe(3);
        result.ErrorDetail.EndColumn.ShouldBe(13);
    }

    // Each engine resolves the missing table only when that statement runs, after the first result set
    // has been returned, so this checks the provider reads past the first result set.
    private const string LaterStatementFails = $"SELECT id FROM {TestTable};\nSELECT id FROM missing_table";

    [Fact]
    public async Task FailureInALaterStatement_IsReported()
    {
        // Arrange
        await InsertRowAsync(1, "first");

        // Act
        var result = await Provider.ExecuteQueryAsync(DatabaseConnectionString, LaterStatementFails, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Kind.ShouldBe(QueryErrorKind.UnknownTable);
        result.ErrorDetail.Token.ShouldNotBeNull().ShouldEndWith("missing_table");
    }

    [Fact]
    public async Task Transaction_FailureInALaterStatement_IsReported()
    {
        // Arrange
        await InsertRowAsync(1, "first");
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);

        // Act
        var result = await Provider.ExecuteInTransactionAsync(
            DatabaseConnectionString, LaterStatementFails, transaction.Id, CancellationToken.None);
        await Provider.RollbackTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNull();
        result.ErrorDetail.Kind.ShouldBe(QueryErrorKind.UnknownTable);
    }

    [Fact]
    public async Task Transaction_Rollback_ShouldLeaveDataUnchanged()
    {
        // Arrange
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);

        // Act
        var insert = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"INSERT INTO {TestTable} (id, name) VALUES (1, 'pending')", transaction.Id, CancellationToken.None);
        var countInside = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"SELECT COUNT(*) AS row_count FROM {TestTable}", transaction.Id, CancellationToken.None);
        await Provider.RollbackTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Assert
        ValidateQueryResult(insert);
        ValidateQueryResult(countInside);
        Convert.ToInt64(countInside.Rows[0]["row_count"]).ShouldBe(1);
        (await CountRowsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Transaction_Commit_ShouldPersistData()
    {
        // Arrange
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);

        // Act
        var first = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"INSERT INTO {TestTable} (id, name) VALUES (1, 'first')", transaction.Id, CancellationToken.None);
        var second = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"INSERT INTO {TestTable} (id, name) VALUES (2, 'second')", transaction.Id, CancellationToken.None);
        await Provider.CommitTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Assert
        ValidateQueryResult(first);
        ValidateQueryResult(second);
        (await CountRowsAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Transaction_Update_ShouldReportRowsAffected()
    {
        // Arrange
        await InsertRowAsync(1, "original");
        await InsertRowAsync(2, "other");
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);

        // Act
        var update = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"UPDATE {TestTable} SET name = 'changed' WHERE id = 1", transaction.Id, CancellationToken.None);
        await Provider.RollbackTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Assert
        ValidateQueryResult(update);
        update.RowsAffected.ShouldBe(1);
    }

    [Fact]
    public async Task Transaction_AfterCommit_ShouldRefuseFurtherStatementsAndCommits()
    {
        // Arrange
        var transaction = await Provider.BeginTransactionAsync(DatabaseConnectionString);
        await Provider.CommitTransactionAsync(DatabaseConnectionString, transaction.Id);

        // Act
        var execute = await Provider.ExecuteInTransactionAsync(DatabaseConnectionString,
            $"INSERT INTO {TestTable} (id, name) VALUES (1, 'late')", transaction.Id, CancellationToken.None);

        // Assert
        execute.Error.ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Provider.CommitTransactionAsync(DatabaseConnectionString, transaction.Id));
        (await CountRowsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ActualPlan_ForUpdate_ShouldCapturePlanAndLeaveRowUnchanged()
    {
        // Arrange
        await InsertRowAsync(1, "original");
        var plans = Provider.ShouldBeAssignableTo<IActualQueryPlanProvider>()!;

        // Act
        var plan = await plans.GetActualPlanAsync(DatabaseConnectionString, ActualPlanUpdateStatement, CancellationToken.None);

        // Assert
        plan.PlanType.ShouldBe("Actual");
        plan.PlanContent.ShouldNotBeNullOrWhiteSpace();
        plan.PlanContent.ShouldNotStartWith("Error");
        (await ReadNameAsync(1)).ShouldBe("original");
    }

    [Fact]
    public async Task ActualPlan_WithCommitInText_ShouldBeRefusedWithoutRunningAnything()
    {
        // Arrange
        await InsertRowAsync(1, "original");
        var plans = Provider.ShouldBeAssignableTo<IActualQueryPlanProvider>()!;

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => plans.GetActualPlanAsync(
            DatabaseConnectionString, $"SELECT 1; COMMIT; DELETE FROM {TestTable}", CancellationToken.None));
        (await CountRowsAsync()).ShouldBe(1);
    }
} 