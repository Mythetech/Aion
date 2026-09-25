using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Aion.Web.Onboarding;
using Aion.Web.Services;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Web.Onboarding;

public class SampleDatabaseProvisionerTests
{
    private readonly InBrowserEngines _engines = new();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly SampleDatabaseProvisioner _sut;

    public SampleDatabaseProvisionerTests()
    {
        _connectionState = new ConnectionState(_engines.ConnectionService, _engines.Factory, _bus, Substitute.For<ILogger<ConnectionState>>());
        _queryState = new QueryState(_bus, Substitute.For<IQuerySaveService>());
        _sut = new SampleDatabaseProvisioner(
            _engines.Factory,
            _connectionState,
            _queryState,
            new IndexedDbStorageService(new JsModuleFake().Runtime),
            _bus);
    }

    [Theory]
    [InlineData(DatabaseType.WasmSQLite)]
    [InlineData(DatabaseType.WasmPostgreSQL)]
    public async Task ProvisionAsync_LoadsTheSampleIntoTheChosenEngine(DatabaseType engine)
    {
        var connection = await _sut.ProvisionAsync(engine);

        connection.Type.ShouldBe(engine);
        _connectionState.Connections.ShouldHaveSingleItem().ShouldBe(connection);
        await ((IManagedDatabaseProvider)_engines.For(engine)).Received(1).EnsureDatabaseAsync(SampleDatabase.Name);
        await _engines.Other(engine).DidNotReceiveWithAnyArgs().ExecuteQueryAsync(default!, default!, default);
    }

    [Fact]
    public async Task ProvisionAsync_ShowsTheSampleQueryInTheEditor()
    {
        var connection = await _sut.ProvisionAsync(DatabaseType.WasmSQLite);

        var sampleTab = _queryState.Active.ShouldNotBeNull();
        sampleTab.Name.ShouldBe("Sample: Products by Price");
        sampleTab.ConnectionId.ShouldBe(connection.Id);
        sampleTab.Query.ShouldBe(SampleDatabase.GetSqliteSampleQueries()[0]);
        await _bus.Received(1).PublishAsync(Arg.Is<FocusQuery>(f => f.Query == sampleTab));
    }

    [Fact]
    public async Task ProvisionAsync_Twice_ReusesTheSampleConnection()
    {
        var first = await _sut.ProvisionAsync(DatabaseType.WasmPostgreSQL);
        var second = await _sut.ProvisionAsync(DatabaseType.WasmPostgreSQL);

        second.ShouldBe(first);
        _connectionState.Connections.ShouldHaveSingleItem();
        _queryState.Active.ShouldNotBeNull().ConnectionId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task ProvisionAsync_OnEachEngine_KeepsTheirConnectionsSeparate()
    {
        await _sut.ProvisionAsync(DatabaseType.WasmSQLite);
        await _sut.ProvisionAsync(DatabaseType.WasmPostgreSQL);

        _connectionState.Connections.Select(c => c.Type)
            .ShouldBe([DatabaseType.WasmSQLite, DatabaseType.WasmPostgreSQL], ignoreOrder: true);
    }

    public static TheoryData<string> SampleStatements()
    {
        var data = new TheoryData<string>();
        foreach (var sql in SampleDatabase.GetSqliteSchema()
                     .Concat(SampleDatabase.GetSqliteSeedData())
                     .Concat(SampleDatabase.GetPostgresSchema())
                     .Concat(SampleDatabase.GetPostgresSeedData()))
        {
            data.Add(sql);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SampleStatements))]
    public void SampleStatements_CanRunAgainstAnExistingSample(string sql)
    {
        if (sql.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            sql.ShouldContain("CREATE TABLE IF NOT EXISTS");

        if (sql.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase))
            sql.TrimEnd().ShouldEndWith("ON CONFLICT DO NOTHING");
    }
}
