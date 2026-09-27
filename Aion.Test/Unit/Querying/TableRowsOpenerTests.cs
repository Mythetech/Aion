using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Core.Database.LiteDB;
using Aion.Core.Database.SqlServer;
using Aion.Web.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;
using Aion.Test.TestDoubles;

namespace Aion.Test.Unit.Querying;

public class TableRowsOpenerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IDatabaseProviderFactory _factory = Substitute.For<IDatabaseProviderFactory>();
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly TableRowsOpener _sut;

    public TableRowsOpenerTests()
    {
        _connections = new ConnectionState(Substitute.For<IConnectionService>(), _factory, _bus, NullLogger<ConnectionState>.Instance, new ConnectionSecretStoreFake());
        _queries = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _sut = new TableRowsOpener(_connections, _queries, _bus);
    }

    private ConnectionModel Connect(DatabaseType type, IStandardDatabaseCommands commands)
    {
        var provider = Substitute.For<IDatabaseProvider>();
        provider.DatabaseType.Returns(type);
        provider.Commands.Returns(commands);
        _factory.GetProvider(type).Returns(provider);

        var connection = new ConnectionModel { Name = "shop", Type = type, ConnectionString = "shop", Active = true };
        _connections.Connections.Add(connection);
        return connection;
    }

    private List<object> Published() => _bus.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IMessageBus.PublishAsync))
        .Select(c => c.GetArguments()[0]!)
        .ToList();

    [Theory]
    [InlineData(DatabaseType.WasmSQLite, "", "SELECT * FROM \"products\"\nLIMIT 1000;")]
    [InlineData(DatabaseType.SQLServer, "dbo", "SELECT TOP (1000) * FROM [dbo].[products];")]
    [InlineData(DatabaseType.LiteDB, "", "SELECT $ FROM products LIMIT 1000")]
    public async Task OpensTheEnginesOwnRowLimitedSelect(DatabaseType type, string schema, string expected)
    {
        IStandardDatabaseCommands commands = type switch
        {
            DatabaseType.SQLServer => new SqlServerCommands(),
            DatabaseType.LiteDB => new LiteDBCommands(),
            _ => new SqliteWasmCommands()
        };
        var connection = Connect(type, commands);

        await _sut.Consume(new OpenTableRows(connection.Id, "shop", schema, "products"));

        _queries.Active!.Query.ShouldBe(expected);
    }

    [Fact]
    public async Task NamesTheTabWithoutSqlServerWording()
    {
        var connection = Connect(DatabaseType.WasmSQLite, new SqliteWasmCommands());

        await _sut.Consume(new OpenTableRows(connection.Id, "shop", "", "products"));

        _queries.Active!.Name.ShouldBe("First 1000 rows - products");
    }

    [Fact]
    public async Task RunsTheQueryAfterTheEditorHasTheNewTab()
    {
        var connection = Connect(DatabaseType.WasmSQLite, new SqliteWasmCommands());

        await _sut.Consume(new OpenTableRows(connection.Id, "shop", "", "products"));

        var query = _queries.Active!;
        query.ConnectionId.ShouldBe(connection.Id);
        query.DatabaseName.ShouldBe("shop");
        var published = Published();
        published.OfType<FocusQuery>().ShouldHaveSingleItem().Query.ShouldBeSameAs(query);
        published.OfType<RunQuery>().ShouldHaveSingleItem();
        published.FindIndex(m => m is FocusQuery).ShouldBeLessThan(published.FindIndex(m => m is RunQuery));
    }

    private static List<ColumnInfo> OrderColumns() =>
    [
        new() { Name = "id", IsPrimaryKey = true },
        new()
        {
            Name = "customer_id",
            ForeignKey = new ForeignKeyInfo { ColumnName = "customer_id", ReferencedTable = "customers", ReferencedColumn = "id" }
        }
    ];

    private (ConnectionModel Connection, IDatabaseProvider Provider) ConnectToShop()
    {
        var connection = Connect(DatabaseType.WasmSQLite, new SqliteWasmCommands());
        connection.Databases.Add(new DatabaseModel { Name = "shop" });
        return (connection, _factory.GetProvider(DatabaseType.WasmSQLite));
    }

    [Fact]
    public async Task RowsOfOneTable_KeepItsColumnsSoTheirForeignKeysLink()
    {
        var (connection, provider) = ConnectToShop();
        provider.GetColumnsAsync(Arg.Any<string>(), "shop", "", "orders").Returns(OrderColumns());

        await _sut.Consume(new OpenTableRows(connection.Id, "shop", "", "orders"));

        var source = _queries.Active!.EditMetadata.ShouldNotBeNull();
        source.IsEditMode.ShouldBeFalse();
        (source.ConnectionId, source.SourceDatabase, source.SourceSchema, source.SourceTable).ShouldBe((connection.Id, "shop", "", "orders"));
        source.ColumnMetadata.Single(c => c.IsForeignKey).ForeignKey!.ReferencedTable.ShouldBe("customers");
    }

    [Fact]
    public async Task ColumnsThatCannotBeRead_StillOpenTheRows_WithoutLinks()
    {
        var (connection, provider) = ConnectToShop();
        provider.GetColumnsAsync(Arg.Any<string>(), "shop", "", "orders")
            .Returns<List<ColumnInfo>>(_ => throw new InvalidOperationException("permission denied for table orders"));

        await _sut.Consume(new OpenTableRows(connection.Id, "shop", "", "orders"));

        _queries.Active!.EditMetadata.ShouldBeNull();
        Published().OfType<RunQuery>().ShouldHaveSingleItem();
        Published().OfType<AddNotification>().ShouldBeEmpty();
    }

    [Fact]
    public async Task ConnectionThatIsGone_OpensNothing()
    {
        var tabs = _queries.Queries.Count;

        await _sut.Consume(new OpenTableRows(Guid.NewGuid(), "shop", "", "products"));

        _queries.Queries.Count.ShouldBe(tabs);
        Published().OfType<AddNotification>().ShouldHaveSingleItem();
        Published().OfType<RunQuery>().ShouldBeEmpty();
    }
}
