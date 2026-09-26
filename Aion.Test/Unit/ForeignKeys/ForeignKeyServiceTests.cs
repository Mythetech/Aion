using Aion.Components.Connections;
using Aion.Components.ForeignKeys;
using Aion.Components.RequestContextPanel;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.ForeignKeys;

public class ForeignKeyServiceTests
{
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly ConnectionState _connections;
    private readonly ForeignKeyService _sut;
    private readonly List<string> _executed = [];

    public ForeignKeyServiceTests()
    {
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), _factory, Substitute.For<IMessageBus>(),
            NullLogger<ConnectionState>.Instance);
        _sut = new ForeignKeyService(_connections);
    }

    private ConnectionModel Connect(DatabaseType type, SqlDialect? dialect, QueryResult? result = null)
    {
        var provider = dialect is null
            ? Substitute.For<IDatabaseProvider>()
            : Substitute.For<IDatabaseProvider, ISqlDialectProvider>();
        provider.DatabaseType.Returns(type);
        if (dialect is not null)
            ((ISqlDialectProvider)provider).Dialect.Returns(dialect);
        provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(1));
        provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _executed.Add(ci.ArgAt<string>(1));
                return result ?? new QueryResult();
            });
        _factory.GetProvider(type).Returns(provider);

        var connection = new ConnectionModel { Name = "shop", Type = type, ConnectionString = "shop", Active = true };
        _connections.Connections.Add(connection);
        return connection;
    }

    private static ForeignKeyDetail Detail(ConnectionModel connection, object value, string? schema = null) =>
        new("Orders", "customer_id", "customers", "id", value, connection.Id, "shop", schema);

    private static QueryResult OneCustomer() => new()
    {
        Columns = ["id", "name"],
        Rows = [new Dictionary<string, object> { ["id"] = 7L, ["name"] = "Ada" }]
    };

    [Fact]
    public async Task InBrowserSqlite_LooksUpTheRowWithItsOwnQuoting()
    {
        var connection = Connect(DatabaseType.WasmSQLite, SqliteDialect.Instance, OneCustomer());

        var lookup = await _sut.FetchReferencedRowAsync(Detail(connection, 7L));

        lookup.Row!["name"].ShouldBe("Ada");
        _executed.ShouldBe(["SELECT * FROM \"customers\"\nWHERE \"id\" = 7\nLIMIT 1;"]);
    }

    [Fact]
    public async Task InBrowserPostgres_QualifiesTheReferencedSchema()
    {
        var connection = Connect(DatabaseType.WasmPostgreSQL, PostgreSqlDialect.Instance, OneCustomer());

        await _sut.FetchReferencedRowAsync(Detail(connection, 7L, schema: "sales"));

        _executed.ShouldBe(["SELECT * FROM \"sales\".\"customers\"\nWHERE \"id\" = 7\nLIMIT 1;"]);
    }

    [Fact]
    public async Task SqlServer_UsesTop()
    {
        var connection = Connect(DatabaseType.SQLServer, SqlServerDialect.Instance, OneCustomer());

        await _sut.FetchReferencedRowAsync(Detail(connection, 7, schema: "dbo"));

        _executed.ShouldBe(["SELECT TOP (1) * FROM [dbo].[customers]\nWHERE [id] = 7;"]);
    }

    [Fact]
    public async Task TextValues_AreEscapedAsLiterals()
    {
        var connection = Connect(DatabaseType.MySQL, MySqlDialect.Instance, OneCustomer());

        await _sut.FetchReferencedRowAsync(Detail(connection, @"O'Brien\"));

        _executed.ShouldBe(["SELECT * FROM `customers`\nWHERE `id` = 'O''Brien\\\\'\nLIMIT 1;"]);
    }

    [Fact]
    public async Task NoMatchingRow_ReturnsNeitherARowNorAnError()
    {
        var connection = Connect(DatabaseType.WasmSQLite, SqliteDialect.Instance, new QueryResult { Columns = ["id"] });

        var lookup = await _sut.FetchReferencedRowAsync(Detail(connection, 7L));

        lookup.Row.ShouldBeNull();
        lookup.Error.ShouldBeNull();
    }

    [Fact]
    public async Task FailedLookup_ReturnsTheEngineError()
    {
        var connection = Connect(DatabaseType.WasmSQLite, SqliteDialect.Instance, new QueryResult { Error = "no such table: customers" });

        var lookup = await _sut.FetchReferencedRowAsync(Detail(connection, 7L));

        lookup.Error.ShouldBe("no such table: customers");
    }

    [Fact]
    public async Task EngineWithoutASqlDialect_SaysLookupsAreNotSupported()
    {
        var connection = Connect(DatabaseType.LiteDB, dialect: null);

        var lookup = await _sut.FetchReferencedRowAsync(Detail(connection, 7L));

        lookup.Error.ShouldNotBeNull();
        _executed.ShouldBeEmpty();
    }

    [Fact]
    public void LookupQuery_WithoutALimit_SelectsEveryMatchingRow()
    {
        var connection = Connect(DatabaseType.WasmPostgreSQL, PostgreSqlDialect.Instance);

        var query = _sut.BuildLookupQuery(Detail(connection, 7L, schema: "public"));

        query.Sql.ShouldBe("SELECT * FROM \"public\".\"customers\"\nWHERE \"id\" = 7;");
    }

    [Fact]
    public void LookupQuery_ForAConnectionThatIsGone_ReturnsAnError()
    {
        var query = _sut.BuildLookupQuery(new ForeignKeyDetail("Orders", "customer_id", "customers", "id", 7, Guid.NewGuid(), "shop"));

        query.Sql.ShouldBeNull();
        query.Error.ShouldNotBeNull();
    }
}
