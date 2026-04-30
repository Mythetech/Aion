using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Scaffolding;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Web.Providers;

namespace Aion.Web.Services;

public class SchemaExecutor
{
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly PGliteProvider _pgliteProvider;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;

    public SchemaExecutor(
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

    public async Task<ConnectionModel> ExecuteAsync(SchemaWizardModel model)
    {
        var (provider, connectionString) = model.EngineType switch
        {
            DatabaseType.WasmSQLite => ((IDatabaseProvider)_sqliteProvider, $"Data Source={model.DatabaseName};Mode=Memory;Cache=Shared"),
            DatabaseType.WasmPostgreSQL => (_pgliteProvider, $"pglite://{model.DatabaseName}"),
            _ => throw new NotSupportedException($"Unsupported engine type: {model.EngineType}")
        };

        switch (model.EngineType)
        {
            case DatabaseType.WasmSQLite:
                await _sqliteProvider.EnsureDatabaseAsync(model.DatabaseName);
                break;
            case DatabaseType.WasmPostgreSQL:
                await _pgliteProvider.EnsureDatabaseAsync(model.DatabaseName);
                break;
        }

        foreach (var table in model.Tables)
        {
            var columns = table.Columns.Select(c => new ColumnDefinition(
                c.Name,
                c.DataType,
                c.IsNullable,
                string.IsNullOrWhiteSpace(c.DefaultValue) ? null : c.DefaultValue,
                c.IsPrimaryKey
            ));

            var ddl = await provider.Commands.GenerateCreateTableScript(
                model.DatabaseName, "", table.Name, columns);

            await provider.ExecuteQueryAsync(connectionString, ddl, CancellationToken.None);
        }

        var connection = new ConnectionModel
        {
            Name = model.DatabaseName,
            ConnectionString = connectionString,
            Type = model.EngineType,
            Active = true,
            IsSavedConnection = false
        };

        _connectionState.Connections.Add(connection);
        await _connectionState.RefreshDatabaseAsync(connection);

        var query = _queryState.AddQuery(model.DatabaseName);
        query.ConnectionId = connection.Id;
        query.DatabaseName = model.DatabaseName;
        _queryState.SetActive(query);

        return connection;
    }
}
