using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Web.Services;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Onboarding;

public class SampleDatabaseProvisioner
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IndexedDbStorageService _storage;
    private readonly IMessageBus _bus;

    public SampleDatabaseProvisioner(
        IDatabaseProviderFactory providerFactory,
        ConnectionState connectionState,
        QueryState queryState,
        IndexedDbStorageService storage,
        IMessageBus bus)
    {
        _providerFactory = providerFactory;
        _connectionState = connectionState;
        _queryState = queryState;
        _storage = storage;
        _bus = bus;
    }

    public async Task<ConnectionModel> ProvisionAsync(DatabaseType engine)
    {
        var name = SampleDatabase.Name;

        var connectionString = engine switch
        {
            DatabaseType.WasmSQLite => $"Data Source={name};Mode=Memory;Cache=Shared",
            DatabaseType.WasmPostgreSQL => $"pglite://{name}",
            _ => throw new NotSupportedException($"Unsupported engine: {engine}")
        };

        var provider = _providerFactory.GetProvider(engine);
        if (provider is IManagedDatabaseProvider managed)
            await managed.EnsureDatabaseAsync(name);

        var schema = engine == DatabaseType.WasmSQLite
            ? SampleDatabase.GetSqliteSchema()
            : SampleDatabase.GetPostgresSchema();

        var seedData = engine == DatabaseType.WasmSQLite
            ? SampleDatabase.GetSqliteSeedData()
            : SampleDatabase.GetPostgresSeedData();

        foreach (var ddl in schema)
            await ExecuteAsync(provider, connectionString, ddl);

        foreach (var dml in seedData)
            await ExecuteAsync(provider, connectionString, dml);

        var connection = await FindSampleConnectionAsync(provider, engine, name);
        if (connection is null)
        {
            connection = new ConnectionModel
            {
                Name = name,
                ConnectionString = connectionString,
                Type = engine,
                Active = true,
                IsSavedConnection = false
            };

            await _connectionState.ConnectAsync(connection);
        }
        else
        {
            await _connectionState.RefreshDatabaseAsync(connection);
        }

        var queries = engine == DatabaseType.WasmSQLite
            ? SampleDatabase.GetSqliteSampleQueries()
            : SampleDatabase.GetPostgresSampleQueries();
        if (queries.Length > 0)
        {
            var query = _queryState.AddQuery("Sample: Products by Price");
            query.ConnectionId = connection.Id;
            query.DatabaseName = name;
            query.Query = queries[0];

            // The editor only swaps in a tab's text when it is told to focus that tab.
            await _bus.PublishAsync(new FocusQuery(query));
        }

        await _storage.SaveDatabaseMetaAsync(name, engine);

        return connection;
    }

    // Loading the sample again reuses its connection rather than adding a second one to the same database.
    private async Task<ConnectionModel?> FindSampleConnectionAsync(IDatabaseProvider provider, DatabaseType engine, string name)
    {
        foreach (var connection in _connectionState.Connections.Where(c => c.Type == engine))
        {
            var databases = await provider.GetDatabasesAsync(connection.ConnectionString) ?? [];
            if (databases.Contains(name))
                return connection;
        }

        return null;
    }

    private static async Task ExecuteAsync(IDatabaseProvider provider, string connectionString, string sql)
    {
        var result = await provider.ExecuteQueryAsync(connectionString, sql, CancellationToken.None);
        if (result.Error is not null)
            throw new InvalidOperationException(result.Error);
    }
}
