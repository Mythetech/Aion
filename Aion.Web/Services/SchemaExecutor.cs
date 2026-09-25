using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Scaffolding;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Services;

public class SchemaExecutor
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ConnectionState _connectionState;
    private readonly QueryState _queryState;
    private readonly IndexedDbStorageService _storage;
    private readonly IMessageBus _bus;

    public SchemaExecutor(
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

    public async Task<ConnectionModel> ExecuteAsync(SchemaWizardModel model)
    {
        var connectionString = model.EngineType switch
        {
            DatabaseType.WasmSQLite => $"Data Source={model.DatabaseName};Mode=Memory;Cache=Shared",
            DatabaseType.WasmPostgreSQL => $"pglite://{model.DatabaseName}",
            _ => throw new NotSupportedException($"Unsupported engine type: {model.EngineType}")
        };

        var provider = _providerFactory.GetProvider(model.EngineType);
        if (provider is IManagedDatabaseProvider managed)
            await managed.EnsureDatabaseAsync(model.DatabaseName);

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

        await _connectionState.ConnectAsync(connection);

        var query = _queryState.AddQuery(model.DatabaseName);
        query.ConnectionId = connection.Id;
        query.DatabaseName = model.DatabaseName;

        // The editor only swaps in a tab's text when it is told to focus that tab.
        await _bus.PublishAsync(new FocusQuery(query));

        await _storage.SaveDatabaseMetaAsync(model.DatabaseName, model.EngineType);

        return connection;
    }
}
