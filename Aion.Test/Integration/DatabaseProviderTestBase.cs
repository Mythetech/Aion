using Aion.Core.Database;
using Aion.Core.Queries;
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
} 