using Aion.Contracts.Queries;
using Aion.Core.Database;
using Aion.Web.Onboarding;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Aion.Test.Integration;

public class SampleDatabasePostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private readonly PostgreSqlProvider _provider = new();
    private string _connectionString = string.Empty;

    public SampleDatabasePostgresTests()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithPassword("postgres")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        foreach (var ddl in SampleDatabase.GetPostgresSchema())
            await ExecuteAsync(ddl);

        foreach (var dml in SampleDatabase.GetPostgresSeedData())
            await ExecuteAsync(dml);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task<QueryResult> ExecuteAsync(string sql)
    {
        var result = await _provider.ExecuteQueryAsync(_connectionString, sql, CancellationToken.None);
        result.Error.ShouldBeNull($"SQL failed: {result.Error}\n{sql}");
        return result;
    }

    private async Task<long> CountAsync(string table)
    {
        var result = await ExecuteAsync($"SELECT COUNT(*) AS count FROM \"{table}\"");
        return Convert.ToInt64(result.Rows[0]["count"]);
    }

    [Fact]
    public async Task SeedData_PopulatesAllTables()
    {
        (await CountAsync("categories")).ShouldBe(5);
        (await CountAsync("products")).ShouldBe(15);
        (await CountAsync("customers")).ShouldBe(10);
        (await CountAsync("orders")).ShouldBe(15);
        (await CountAsync("order_items")).ShouldBe(25);
    }

    [Fact]
    public async Task SampleQueries_ExecuteWithoutErrors()
    {
        foreach (var query in SampleDatabase.GetPostgresSampleQueries())
        {
            var result = await ExecuteAsync(query);
            result.Rows.ShouldNotBeEmpty($"Sample query returned no rows: {query}");
        }
    }

    [Fact]
    public async Task Inserts_AfterSeeding_DoNotCollideWithSeededIds()
    {
        await ExecuteAsync("INSERT INTO \"categories\" (\"name\", \"description\") VALUES ('Toys', 'Games and toys')");
        await ExecuteAsync("INSERT INTO \"products\" (\"name\", \"category_id\", \"price\") VALUES ('Chess Set', 1, 24.99)");
        await ExecuteAsync("INSERT INTO \"customers\" (\"name\", \"email\", \"created_at\") VALUES ('Kevin Foster', 'kevin@example.com', '2024-11-01')");
        await ExecuteAsync("INSERT INTO \"orders\" (\"customer_id\", \"order_date\") VALUES (1, '2024-11-02')");
        await ExecuteAsync("INSERT INTO \"order_items\" (\"order_id\", \"product_id\", \"quantity\", \"unit_price\") VALUES (1, 1, 1, 79.99)");
    }
}
