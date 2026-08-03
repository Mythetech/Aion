using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Web.Providers;
using Aion.Web.Services;

namespace Aion.Web.Onboarding;

public class SampleDatabaseProvisioner
{
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly PGliteProvider _pgliteProvider;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IndexedDbStorageService _storage;

    public SampleDatabaseProvisioner(
        SqliteWasmProvider sqliteProvider,
        PGliteProvider pgliteProvider,
        ConnectionState connectionState,
        QueryState queryState,
        IndexedDbStorageService storage)
    {
        _sqliteProvider = sqliteProvider;
        _pgliteProvider = pgliteProvider;
        _connectionState = connectionState;
        _queryState = queryState;
        _storage = storage;
    }

    public async Task<ConnectionModel> ProvisionAsync(DatabaseType engine)
    {
        var name = SampleDatabase.Name;

        var (provider, connectionString) = engine switch
        {
            DatabaseType.WasmSQLite => ((IDatabaseProvider)_sqliteProvider, $"Data Source={name};Mode=Memory;Cache=Shared"),
            DatabaseType.WasmPostgreSQL => (_pgliteProvider, $"pglite://{name}"),
            _ => throw new NotSupportedException($"Unsupported engine: {engine}")
        };

        switch (engine)
        {
            case DatabaseType.WasmSQLite:
                await _sqliteProvider.EnsureDatabaseAsync(name);
                break;
            case DatabaseType.WasmPostgreSQL:
                await _pgliteProvider.EnsureDatabaseAsync(name);
                break;
        }

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

        var connection = new ConnectionModel
        {
            Name = name,
            ConnectionString = connectionString,
            Type = engine,
            Active = true,
            IsSavedConnection = false
        };

        await _connectionState.ConnectAsync(connection);

        var queries = engine == DatabaseType.WasmSQLite
            ? SampleDatabase.GetSqliteSampleQueries()
            : SampleDatabase.GetPostgresSampleQueries();
        if (queries.Length > 0)
        {
            var query = _queryState.AddQuery("Sample: Products by Price");
            query.ConnectionId = connection.Id;
            query.DatabaseName = name;
            query.Query = queries[0];
            _queryState.SetActive(query);
        }

        await _storage.SaveDatabaseMetaAsync(name, engine);

        return connection;
    }

    private static async Task ExecuteAsync(IDatabaseProvider provider, string connectionString, string sql)
    {
        var result = await provider.ExecuteQueryAsync(connectionString, sql, CancellationToken.None);
        if (result.Error is not null)
            throw new InvalidOperationException(result.Error);
    }
}
