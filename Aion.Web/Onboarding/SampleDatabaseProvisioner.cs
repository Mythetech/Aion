using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Web.Providers;

namespace Aion.Web.Onboarding;

public class SampleDatabaseProvisioner
{
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly PGliteProvider _pgliteProvider;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;

    public SampleDatabaseProvisioner(
        SqliteWasmProvider sqliteProvider,
        PGliteProvider pgliteProvider,
        ConnectionState connectionState,
        QueryState queryState)
    {
        _sqliteProvider = sqliteProvider;
        _pgliteProvider = pgliteProvider;
        _connectionState = connectionState;
        _queryState = queryState;
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

        foreach (var ddl in schema)
            await provider.ExecuteQueryAsync(connectionString, ddl, CancellationToken.None);

        foreach (var dml in SampleDatabase.GetSeedData())
            await provider.ExecuteQueryAsync(connectionString, dml, CancellationToken.None);

        var connection = new ConnectionModel
        {
            Name = name,
            ConnectionString = connectionString,
            Type = engine,
            Active = true,
            IsSavedConnection = false
        };

        _connectionState.Connections.Add(connection);
        await _connectionState.RefreshDatabaseAsync(connection);

        var queries = SampleDatabase.GetSampleQueries();
        if (queries.Length > 0)
        {
            var query = _queryState.AddQuery("Sample: Products by Price");
            query.ConnectionId = connection.Id;
            query.DatabaseName = name;
            query.Query = queries[0];
            _queryState.SetActive(query);
        }

        return connection;
    }
}
