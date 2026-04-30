using Aion.Components.Connections;
using Aion.Components.Scaffolding;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Web.Providers;

namespace Aion.Web.Services;

public class SchemaExecutor
{
    private readonly SqliteWasmProvider _sqliteProvider;
    private readonly PGliteProvider _pgliteProvider;
    private readonly IConnectionService _connectionService;

    public SchemaExecutor(
        SqliteWasmProvider sqliteProvider,
        PGliteProvider pgliteProvider,
        IConnectionService connectionService)
    {
        _sqliteProvider = sqliteProvider;
        _pgliteProvider = pgliteProvider;
        _connectionService = connectionService;
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

        await _connectionService.AddConnection(connection);
        return connection;
    }
}
