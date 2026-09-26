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
} 