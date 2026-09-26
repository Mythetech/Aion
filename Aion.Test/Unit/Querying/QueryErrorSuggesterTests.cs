using Aion.Components.Connections;
using Aion.Components.Querying.Errors;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryErrorSuggesterTests
{
    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly IDatabaseProvider _provider = Substitute.For<IDatabaseProvider>();
    private readonly ConnectionModel _connection;
    private readonly DatabaseModel _database = new() { Name = "shop" };
    private readonly QueryErrorSuggester _suggester;

    public QueryErrorSuggesterTests()
    {
        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.WasmSQLite).Returns(_provider);
        _provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns("db");

        _connection = new ConnectionModel { Name = "Local", Type = DatabaseType.WasmSQLite, Databases = [_database] };
        var connections = new ConnectionState(_connectionService, factory, Substitute.For<IMessageBus>(), NullLogger<ConnectionState>.Instance)
        {
            Connections = [_connection]
        };
        _suggester = new QueryErrorSuggester(connections, NullLogger<QueryErrorSuggester>.Instance);

        _database.Tables = [new TableInfo("", "products"), new TableInfo("", "categories"), new TableInfo("", "orders")];
        _database.TablesLoaded = true;
        CacheColumns("products", "id", "name", "price", "category_id");
        CacheColumns("categories", "id", "title");
    }

    private void CacheColumns(string table, params string[] columns)
    {
        _database.TableColumns[table] = columns.Select(c => new ColumnInfo { Name = c, DataType = "text" }).ToList();
        _database.LoadedColumnTables.Add(table);
    }

    private Task<ErrorSuggestion?> SuggestAsync(string message, string sql) =>
        _suggester.SuggestAsync(QueryErrorNormalizer.Normalize(message, sql), sql, _connection.Id, _database.Name);

    [Fact]
    public async Task UnknownColumn_SuggestsTheClosestColumnAndItsTable()
    {
        // Act
        var suggestion = await SuggestAsync("no such column: categry_id", "SELECT name FROM products WHERE categry_id = 2");

        // Assert
        suggestion.ShouldBe(new ErrorSuggestion("category_id", "products"));
    }

    [Fact]
    public async Task UnknownColumn_OnlyLooksAtTablesTheSqlUses()
    {
        // Act: "tite" is close to categories.title, but the query only reads products.
        var suggestion = await SuggestAsync("no such column: tite", "SELECT tite FROM products");

        // Assert
        suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task QualifiedUnknownColumn_LooksInTheAliasedTable()
    {
        // Act
        var suggestion = await SuggestAsync("no such column: c.tite",
            "SELECT p.name, c.tite FROM products p JOIN categories c ON c.id = p.category_id");

        // Assert
        suggestion.ShouldBe(new ErrorSuggestion("title", "categories"));
    }

    [Fact]
    public async Task UnknownTable_SuggestsTheClosestTable()
    {
        // Act
        var suggestion = await SuggestAsync("no such table: prodcts", "SELECT * FROM prodcts");

        // Assert
        suggestion.ShouldBe(new ErrorSuggestion("products"));
    }

    [Fact]
    public async Task UnknownTable_ReportedWithTheDatabaseName_StillMatchesTheTable()
    {
        // Act
        var suggestion = await SuggestAsync("Table 'shop.prodcts' doesn't exist", "SELECT * FROM prodcts");

        // Assert
        suggestion.ShouldBe(new ErrorSuggestion("products"));
    }

    [Fact]
    public async Task NoCloseName_SuggestsNothing()
    {
        // Act
        var suggestion = await SuggestAsync("no such column: weight", "SELECT weight FROM products");

        // Assert
        suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task OtherErrors_SuggestNothing()
    {
        // Act
        var suggestion = await SuggestAsync("near \"SELEC\": syntax error", "SELEC name FROM products");

        // Assert
        suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task ColumnsNotYetCached_AreLoadedForTheReferencedTable()
    {
        // Arrange
        _database.TableColumns.Remove("products");
        _database.LoadedColumnTables.Remove("products");
        _provider.GetColumnsAsync(Arg.Any<string>(), "shop", "", "products")
            .Returns([new ColumnInfo { Name = "category_id", DataType = "integer" }]);

        // Act
        var suggestion = await SuggestAsync("no such column: categry_id", "SELECT categry_id FROM products");

        // Assert
        suggestion.ShouldBe(new ErrorSuggestion("category_id", "products"));
    }

    [Fact]
    public async Task ColumnsThatFailToLoad_AreSkipped()
    {
        // Arrange
        _database.TableColumns.Remove("products");
        _database.LoadedColumnTables.Remove("products");
        _provider.GetColumnsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException("offline"));

        // Act
        var suggestion = await SuggestAsync("no such column: categry_id", "SELECT categry_id FROM products");

        // Assert
        suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownConnection_SuggestsNothing()
    {
        // Arrange
        var sql = "SELECT categry_id FROM products";

        // Act
        var suggestion = await _suggester.SuggestAsync(
            QueryErrorNormalizer.Normalize("no such column: categry_id", sql), sql, Guid.NewGuid(), "shop");

        // Assert
        suggestion.ShouldBeNull();
    }
}
