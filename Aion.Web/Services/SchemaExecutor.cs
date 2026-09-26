using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Scaffolding;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Services;

public class SchemaExecutor : ISchemaExecutor
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

    public async Task<SchemaExecutionResult> ExecuteAsync(SchemaWizardModel model)
    {
        var connectionString = model.EngineType switch
        {
            DatabaseType.WasmSQLite => $"Data Source={model.DatabaseName};Mode=Memory;Cache=Shared",
            DatabaseType.WasmPostgreSQL => $"pglite://{model.DatabaseName}",
            _ => null
        };

        if (connectionString is null)
            return SchemaExecutionResult.Failed($"Creating {model.EngineType} databases in the browser is not supported.");

        var provider = _providerFactory.GetProvider(model.EngineType);

        var error = await CreateTablesAsync(provider, connectionString, model);
        if (error is not null)
            return SchemaExecutionResult.Failed(error);

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

        return SchemaExecutionResult.Created(connection);
    }

    /// <summary>
    /// Runs every CREATE TABLE in one transaction, so a failure leaves the database as it was and the user can
    /// fix the definition and finish again without the tables that did succeed getting in the way.
    /// </summary>
    private static async Task<string?> CreateTablesAsync(IDatabaseProvider provider, string connectionString, SchemaWizardModel model)
    {
        TransactionInfo transaction;
        try
        {
            if (provider is IManagedDatabaseProvider managed)
                await managed.EnsureDatabaseAsync(model.DatabaseName);

            transaction = await provider.BeginTransactionAsync(connectionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CouldNotCreate(ex);
        }

        try
        {
            foreach (var table in model.Tables)
            {
                var ddl = await provider.Commands.GenerateCreateTableScript(
                    model.DatabaseName, "", table.Name, ToColumnDefinitions(table));

                var result = await provider.ExecuteInTransactionAsync(connectionString, ddl, transaction.Id, CancellationToken.None);
                if (!result.Success)
                {
                    await provider.RollbackTransactionAsync(connectionString, transaction.Id);
                    return $"Could not create table \"{table.Name}\": {EngineMessage(result)}";
                }
            }

            await provider.CommitTransactionAsync(connectionString, transaction.Id);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RollBackQuietlyAsync(provider, connectionString, transaction.Id);
            return CouldNotCreate(ex);
        }
    }

    private static IEnumerable<ColumnDefinition> ToColumnDefinitions(TableDefinitionModel table) =>
        table.Columns.Select(c => new ColumnDefinition(
            c.Name,
            c.DataType,
            c.IsNullable,
            string.IsNullOrWhiteSpace(c.DefaultValue) ? null : c.DefaultValue,
            c.IsPrimaryKey));

    private static string EngineMessage(QueryResult result) => result.ErrorDetail?.Message ?? result.Error ?? "unknown error";

    private static string CouldNotCreate(Exception ex) => $"Could not create the tables: {ex.Message}";

    // The original failure is what the user needs to see; a rollback that also fails would only hide it.
    private static async Task RollBackQuietlyAsync(IDatabaseProvider provider, string connectionString, string transactionId)
    {
        try
        {
            await provider.RollbackTransactionAsync(connectionString, transactionId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
    }
}
