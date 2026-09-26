using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Components.Shared.Snackbar;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using MudBlazor;
using Aion.Components.Connections.Events;
using Aion.Components.Shared.Snackbar.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aion.Components.Connections;

public class ConnectionState
{
    private readonly IConnectionService _connectionService;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ConnectionState> _logger;
    private readonly ConcurrentDictionary<string, bool> _finishingTransactions = new();

    public ConnectionState(IConnectionService connectionService, IDatabaseProviderFactory providerFactory, IMessageBus bus, ILogger<ConnectionState> logger)
    {
        _connectionService = connectionService;
        _providerFactory = providerFactory;
        _messageBus = bus;
        _logger = logger;
    }
    
    public event Action? ConnectionStateChanged;
    
    protected void OnConnectionStateChanged() => ConnectionStateChanged?.Invoke();
    
    protected async Task NotifyQueryChanged() => await _messageBus.PublishAsync(new QueryChanged());
    
    public List<ConnectionModel> Connections { get; set; } = [];

    public async Task InitializeAsync()
    {
        await _connectionService.InitializeAsync();
        var savedConnections = await _connectionService.GetSavedConnections();
        Connections = savedConnections.ToList();

        foreach (var connection in Connections)
        {
            connection.HealthStatus = ConnectionHealthStatus.Checking;
        }

        OnConnectionStateChanged();

        // Concurrent so several unreachable servers cost one driver timeout at startup, not one each.
        await Task.WhenAll(Connections.ToList().Select(RefreshDatabaseAsync));

        OnConnectionStateChanged();
    }

    /// <summary>
    /// Tries to reach the server and list its databases without changing any state.
    /// </summary>
    public async Task<ConnectionResult> TestConnectionAsync(string connectionString, DatabaseType type)
    {
        try
        {
            var databases = await _connectionService.GetDatabasesAsync(connectionString, type);

            return databases is null
                ? ConnectionResult.Failed("The server did not return a list of databases.")
                : ConnectionResult.Connected(databases);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to {DatabaseType} server", type);
            return ConnectionResult.FromException(ex);
        }
    }

    /// <summary>
    /// Connects a new connection and, only when the server answers, marks it active, saves it and announces it.
    /// </summary>
    public async Task<ConnectionResult> ConnectAsync(ConnectionModel connection)
    {
        var result = await TestConnectionAsync(connection.ConnectionString, connection.Type);
        if (!result.Success)
            return result;

        ApplyConnectionResult(connection, result);

        await _connectionService.AddConnection(connection);
        Connections.Add(connection);

        await _messageBus.PublishAsync(new AddNotification($"Connected to {connection.Name}", Severity.Success));
        OnConnectionStateChanged();

        return result;
    }

    public const string ActualPlanNotice =
        "Actual plan captured. The statement ran inside a transaction that was rolled back, so none of its changes were kept.";

    public const string ActualPlanInTransactionMessage =
        "Actual query plans can't be captured while this tab has an open transaction. Commit or roll back first, or turn off Actual Query Plan.";

    public Task<QueryResult> ExecuteQueryAsync(QueryModel query, CancellationToken cancellationToken) =>
        ExecuteQueryAsync(query, query.Query, cancellationToken);

    /// <summary>
    /// Runs <paramref name="sql"/> as the tab's statement. The tab's own text is never touched, so
    /// running a selection, or editing while a run is in progress, leaves the editor as the user left it.
    /// </summary>
    public async Task<QueryResult> ExecuteQueryAsync(QueryModel query, string sql, CancellationToken cancellationToken)
    {
        var connection = Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
        if (connection == null) return new QueryResult { Error = "Connection not found" };

        var provider = GetProvider(connection.Type);
        var connectionString = provider.UpdateConnectionString(connection.ConnectionString, query.DatabaseName);

        try
        {
            query.StartExecution(sql);
            await NotifyQueryChanged();

            if (query.IncludeEstimatedPlan && provider is IEstimatedQueryPlanProvider estimatedPlans)
            {
                query.EstimatedPlan = await GetEstimatedPlanAsync(estimatedPlans, connectionString, sql, cancellationToken);
                await NotifyQueryChanged();
            }

            if (query.IncludeActualPlan && provider is IActualQueryPlanProvider actualPlans)
            {
                return await CaptureActualPlanAsync(query, actualPlans, connectionString, sql, cancellationToken);
            }

            if (query.UseTransaction && !query.HasOpenTransaction)
            {
                var failedToStart = await TryBeginTransactionAsync(query, connection, provider, connectionString);
                if (failedToStart != null) return failedToStart;
            }

            QueryResult result;
            if (query.Transaction is { Status: TransactionStatus.Active } transaction)
            {
                // Once a transaction is open every run in the tab joins it, even if the toggle was
                // switched off: running outside it could block on the tab's own locks.
                result = await provider.ExecuteInTransactionAsync(connectionString, sql, transaction.Id, cancellationToken);
                if (result.Success)
                {
                    query.Transaction = transaction.WithStatementExecuted();
                }
            }
            else
            {
                result = await provider.ExecuteQueryAsync(connectionString, sql, cancellationToken);
            }

            query.SetResult(result);

            if (!result.Success && (result?.Error?.Contains("Failed to connect") ?? false))
            {
                connection.Active = false;
                OnConnectionStateChanged();
            }
            
            await _messageBus.PublishAsync(new QueryExecuted(query));
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new QueryResult { Error = "Query cancelled", Cancelled = true };
            query.SetResult(result);
            await _messageBus.PublishAsync(new QueryExecuted(query));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            var result = new QueryResult { Error = ex.Message };
            query.SetResult(result);
            await _messageBus.PublishAsync(new QueryExecuted(query));
            return result;
        }
    }

    private static async Task<QueryPlan> GetEstimatedPlanAsync(
        IEstimatedQueryPlanProvider plans, string connectionString, string query, CancellationToken cancellationToken)
    {
        try
        {
            return await plans.GetEstimatedPlanAsync(connectionString, query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new QueryPlan { PlanType = "Estimated", PlanFormat = "TEXT", PlanContent = $"Error getting plan: {ex.Message}" };
        }
    }

    private async Task<QueryResult> CaptureActualPlanAsync(
        QueryModel query, IActualQueryPlanProvider plans, string connectionString, string sql, CancellationToken cancellationToken)
    {
        QueryResult result;
        if (query.HasOpenTransaction)
        {
            result = new QueryResult { Error = ActualPlanInTransactionMessage };
            query.SetResult(result);
        }
        else
        {
            // Cleared first so a failed capture never leaves an earlier statement's plan on screen.
            query.ActualPlan = null;
            query.ActualPlan = await plans.GetActualPlanAsync(connectionString, sql, cancellationToken);
            result = new QueryResult();
            query.SetResult(result, ActualPlanNotice);
        }

        await _messageBus.PublishAsync(new QueryExecuted(query));
        return result;
    }

    /// <summary>
    /// Starts the tab's transaction before its first statement. Returns an error result when BEGIN
    /// fails so the statement is never run outside the transaction the user asked for.
    /// </summary>
    private async Task<QueryResult?> TryBeginTransactionAsync(
        QueryModel query, ConnectionModel connection, IDatabaseProvider provider, string connectionString)
    {
        try
        {
            query.Transaction = await provider.BeginTransactionAsync(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start a transaction for query {QueryId}", query.Id);
            var result = new QueryResult { Error = $"Could not start a transaction, so the statement was not run. {ex.Message}" };
            query.SetResult(result);
            await _messageBus.PublishAsync(new QueryExecuted(query));
            return result;
        }

        _logger.LogInformation("Started transaction {TransactionId} for query {QueryId}", query.Transaction.Value.Id, query.Id);
        await _messageBus.PublishAsync(new TransactionStarted(connection.Id, query.Transaction.Value));
        return null;
    }

    /// <summary>
    /// Commits the tab's open transaction. On failure the transaction stays open in the tab so the
    /// user can still roll it back, and the error is shown as a notification.
    /// </summary>
    public async Task<bool> CommitTransactionAsync(QueryModel query)
    {
        if (query.Transaction is not { Status: TransactionStatus.Active } transaction) return false;
        if (!_finishingTransactions.TryAdd(transaction.Id, true)) return false;

        try
        {
            var connection = Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
            if (connection == null)
            {
                await _messageBus.PublishAsync(new AddNotification(
                    "Commit failed: the connection for this transaction no longer exists.", Severity.Error));
                return false;
            }

            try
            {
                var provider = GetProvider(connection.Type);
                var connectionString = provider.UpdateConnectionString(connection.ConnectionString, query.DatabaseName);
                await provider.CommitTransactionAsync(connectionString, transaction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit transaction {TransactionId}", transaction.Id);
                await _messageBus.PublishAsync(new AddNotification(
                    $"Commit failed: {ex.Message}{Environment.NewLine}Use Rollback to close the transaction.", Severity.Error));
                return false;
            }

            await FinishTransactionAsync(query, connection.Id, transaction.WithStatus(TransactionStatus.Committed), committed: true);
            await _messageBus.PublishAsync(new AddNotification("Transaction committed", Severity.Success));
            return true;
        }
        finally
        {
            _finishingTransactions.TryRemove(transaction.Id, out _);
        }
    }

    /// <summary>
    /// Rolls back the tab's open transaction. Providers release the underlying session even when
    /// ROLLBACK itself fails, so the tab is always cleared and any error is shown as a notification.
    /// </summary>
    public async Task RollbackTransactionAsync(QueryModel query, string successMessage = "Transaction rolled back")
    {
        if (query.Transaction is not { Status: TransactionStatus.Active } transaction) return;
        if (!_finishingTransactions.TryAdd(transaction.Id, true)) return;

        try
        {
            var connection = Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
            string? failure = null;

            if (connection != null)
            {
                try
                {
                    var provider = GetProvider(connection.Type);
                    var connectionString = provider.UpdateConnectionString(connection.ConnectionString, query.DatabaseName);
                    await provider.RollbackTransactionAsync(connectionString, transaction.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to roll back transaction {TransactionId}", transaction.Id);
                    failure = ex.Message;
                }
            }

            await FinishTransactionAsync(query, connection?.Id ?? Guid.Empty, transaction.WithStatus(TransactionStatus.RolledBack), committed: false);

            await _messageBus.PublishAsync(failure == null
                ? new AddNotification(successMessage, Severity.Info)
                : new AddNotification($"Rollback reported an error: {failure}", Severity.Warning));
        }
        finally
        {
            _finishingTransactions.TryRemove(transaction.Id, out _);
        }
    }

    private async Task FinishTransactionAsync(QueryModel query, Guid connectionId, TransactionInfo finished, bool committed)
    {
        query.Transaction = null;
        await _messageBus.PublishAsync(new TransactionFinished(connectionId, query.Id, finished, committed));
        await NotifyQueryChanged();
    }

    /// <summary>
    /// Reconnects and lists the databases again. Databases that are still there keep their models, and whatever
    /// had been loaded for them is loaded again, so an expanded schema tree keeps its shape with current data.
    /// </summary>
    public async Task<ConnectionResult> RefreshDatabaseAsync(ConnectionModel connection)
    {
        var result = await TestConnectionAsync(connection.ConnectionString, connection.Type);
        ApplyConnectionResult(connection, result);
        OnConnectionStateChanged();

        if (result.Success)
        {
            foreach (var database in connection.Databases.ToList())
            {
                await RefreshSchemaAsync(connection, database);
            }
        }

        return result;
    }

    public void MarkHealthCheckStarted(ConnectionModel connection)
    {
        connection.HealthStatus = ConnectionHealthStatus.Checking;
        OnConnectionStateChanged();
    }

    public void ApplyHealthCheck(ConnectionModel connection, ConnectionHealthCheckResult result)
    {
        SetHealth(connection, result.IsHealthy, result.TimedOut, result.ErrorMessage, result.CheckTime);
        OnConnectionStateChanged();
    }

    public void RecordActivity(Guid connectionId)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection != null)
        {
            connection.LastActivityTime = DateTime.UtcNow;
        }
    }

    private static void ApplyConnectionResult(ConnectionModel connection, ConnectionResult result)
    {
        if (result.Success)
        {
            var known = connection.Databases.DistinctBy(db => db.Name).ToDictionary(db => db.Name);
            connection.Databases = result.Databases
                .Select(name => known.GetValueOrDefault(name) ?? new DatabaseModel { Name = name })
                .ToList();
        }

        SetHealth(connection, result.Success, result.TimedOut, result.Error, DateTime.UtcNow);
    }

    private static void SetHealth(ConnectionModel connection, bool healthy, bool timedOut, string? error, DateTime checkedAt)
    {
        connection.Active = healthy;
        connection.LastError = healthy ? null : error;
        connection.LastHealthCheckTime = checkedAt;
        connection.HealthStatus = healthy
            ? ConnectionHealthStatus.Healthy
            : timedOut ? ConnectionHealthStatus.Timeout : ConnectionHealthStatus.Unhealthy;
    }

    public bool SupportsIndexes(DatabaseType type) => _providerFactory.GetProvider(type) is IDatabaseIndexProvider;

    public bool SupportsRoutines(DatabaseType type) => _providerFactory.GetProvider(type) is IDatabaseRoutineProvider;

    public bool SupportsEstimatedPlan(DatabaseType type) => _providerFactory.GetProvider(type) is IEstimatedQueryPlanProvider;

    public bool SupportsActualPlan(DatabaseType type) => _providerFactory.GetProvider(type) is IActualQueryPlanProvider;

    public bool SupportsCreatingTables(DatabaseType type) => _providerFactory.GetProvider(type) is ISqlDialectProvider;

    public bool SupportsCreatingDatabases(DatabaseType type) => _providerFactory.GetProvider(type) is IDatabaseCreationProvider;

    // Every schema load below records a failure on the model instead of throwing: callers range from the
    // schema tree to autocomplete, and each decides whether a failure matters to it by reading the state.

    public Task<SchemaLoadState> LoadTablesAsync(ConnectionModel connection, DatabaseModel database) =>
        database.TablesState.IsLoaded ? Task.FromResult(database.TablesState) : FetchTablesAsync(connection, database);

    public Task<SchemaLoadState> LoadIndexesAsync(ConnectionModel connection, DatabaseModel database) =>
        database.IndexesState.IsLoaded ? Task.FromResult(database.IndexesState) : FetchIndexesAsync(connection, database);

    public Task<SchemaLoadState> LoadRoutinesAsync(ConnectionModel connection, DatabaseModel database) =>
        database.RoutinesState.IsLoaded ? Task.FromResult(database.RoutinesState) : FetchRoutinesAsync(connection, database);

    public Task<SchemaLoadState> LoadColumnsAsync(ConnectionModel connection, DatabaseModel database, string schema, string table) =>
        database.LoadedColumnTables.Contains(new TableInfo(schema, table).DisplayName)
            ? Task.FromResult(SchemaLoadState.Loaded)
            : FetchColumnsAsync(connection, database, new TableInfo(schema, table));

    /// <summary>
    /// Loads again every part of the database's schema that was loaded or failed to load, and forgets the
    /// columns of tables that no longer exist. Parts nobody asked for stay unloaded.
    /// </summary>
    public async Task RefreshSchemaAsync(ConnectionModel connection, DatabaseModel database)
    {
        var columnTables = database.LoadedColumnTables.Concat(database.ColumnStates.Keys).Distinct().ToList();

        if (database.TablesState.Status != SchemaLoadStatus.NotLoaded)
        {
            await FetchTablesAsync(connection, database);
        }

        var tables = database.TablesState.IsLoaded
            ? database.Tables.DistinctBy(t => t.DisplayName).ToDictionary(t => t.DisplayName)
            : [];

        // Sequential on purpose: the loads share the model's collections, and a refresh only reloads what
        // the user has open.
        var forgotColumns = false;
        foreach (var key in columnTables)
        {
            if (tables.TryGetValue(key, out var table))
            {
                await FetchColumnsAsync(connection, database, table);
            }
            else
            {
                ForgetColumns(database, key);
                forgotColumns = true;
            }
        }

        if (database.IndexesState.Status != SchemaLoadStatus.NotLoaded)
        {
            await FetchIndexesAsync(connection, database);
        }

        if (database.RoutinesState.Status != SchemaLoadStatus.NotLoaded)
        {
            await FetchRoutinesAsync(connection, database);
        }

        if (forgotColumns)
        {
            OnConnectionStateChanged();
        }
    }

    /// <summary>
    /// Brings the schema up to date after a statement that changed it ran on the connection. A change to the
    /// databases lists them again; anything else reloads what was loaded for the database it ran against.
    /// </summary>
    public async Task RefreshAfterSchemaChangeAsync(Guid connectionId, string? databaseName, bool databasesChanged)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection == null) return;

        if (databasesChanged)
        {
            await RefreshDatabaseAsync(connection);
            return;
        }

        var database = connection.Databases.FirstOrDefault(d => d.Name == databaseName);
        if (database != null)
        {
            await RefreshSchemaAsync(connection, database);
        }
    }

    private async Task<SchemaLoadState> FetchTablesAsync(ConnectionModel connection, DatabaseModel database)
    {
        database.TablesState = SchemaLoadState.Loading;

        try
        {
            var connectionString = GetProvider(connection.Type).UpdateConnectionString(connection.ConnectionString, database.Name);
            database.Tables = await _connectionService.GetTablesAsync(connectionString, database.Name, connection.Type);
            database.TablesState = SchemaLoadState.Loaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the tables of database {Database}", database.Name);
            database.TablesState = SchemaLoadFailure(ex);
        }

        OnConnectionStateChanged();
        return database.TablesState;
    }

    private async Task<SchemaLoadState> FetchColumnsAsync(ConnectionModel connection, DatabaseModel database, TableInfo table)
    {
        var key = table.DisplayName;
        database.ColumnStates[key] = SchemaLoadState.Loading;

        try
        {
            var provider = GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database.Name);
            var columns = await provider.GetColumnsAsync(connectionString, database.Name, table.Schema, table.Name);

            database.TableColumns[key] = columns;
            database.LoadedColumnTables.Add(key);
            database.ColumnStates.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the columns of table {Table} in database {Database}", key, database.Name);
            ForgetColumns(database, key);
            database.ColumnStates[key] = SchemaLoadFailure(ex);
        }

        OnConnectionStateChanged();
        return database.ColumnsState(key);
    }

    // The tree has one row for the message, and errors thrown across JS interop append the JavaScript stack
    // after the first line. The full exception is still logged.
    private static SchemaLoadState SchemaLoadFailure(Exception ex)
    {
        var firstLine = ex.Message.Split('\n', 2)[0].Trim();
        return SchemaLoadState.Failed(string.IsNullOrEmpty(firstLine) ? ex.GetType().Name : firstLine);
    }

    private static void ForgetColumns(DatabaseModel database, string key)
    {
        database.TableColumns.Remove(key);
        database.LoadedColumnTables.Remove(key);
        database.ColumnStates.Remove(key);
    }

    private async Task<SchemaLoadState> FetchIndexesAsync(ConnectionModel connection, DatabaseModel database)
    {
        var provider = GetProvider(connection.Type);
        if (provider is not IDatabaseIndexProvider indexProvider)
        {
            // An engine without index metadata has none to show, which is a finished load and not a failure.
            database.IndexesState = SchemaLoadState.Loaded;
            OnConnectionStateChanged();
            return database.IndexesState;
        }

        database.IndexesState = SchemaLoadState.Loading;

        try
        {
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database.Name);
            database.Indexes = await indexProvider.GetIndexesAsync(connectionString, database.Name);
            database.IndexesState = SchemaLoadState.Loaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the indexes of database {Database}", database.Name);
            database.IndexesState = SchemaLoadFailure(ex);
        }

        OnConnectionStateChanged();
        return database.IndexesState;
    }

    private async Task<SchemaLoadState> FetchRoutinesAsync(ConnectionModel connection, DatabaseModel database)
    {
        var provider = GetProvider(connection.Type);
        if (provider is not IDatabaseRoutineProvider routineProvider)
        {
            database.RoutinesState = SchemaLoadState.Loaded;
            OnConnectionStateChanged();
            return database.RoutinesState;
        }

        database.RoutinesState = SchemaLoadState.Loading;

        try
        {
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database.Name);
            database.Routines = await routineProvider.GetRoutinesAsync(connectionString, database.Name);
            database.RoutinesState = SchemaLoadState.Loaded;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the functions of database {Database}", database.Name);
            database.RoutinesState = SchemaLoadFailure(ex);
        }

        OnConnectionStateChanged();
        return database.RoutinesState;
    }

    public async Task RemoveConnection(Guid id)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == id);
        if (connection == null) return;

        Connections.Remove(connection);
        await _connectionService.RemoveConnection(id);
        OnConnectionStateChanged();
        await _messageBus.PublishAsync(new ConnectionRemoved(id));
    }

    /// <summary>
    /// Saves the edited settings and reconnects with them. The edit is kept even when the server is
    /// unreachable, so the result only reports whether the reconnect worked.
    /// </summary>
    public async Task<ConnectionResult> UpdateConnection(Guid id, ConnectionModel updated)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == id);
        if (connection == null) return ConnectionResult.Failed("The connection no longer exists.");

        connection.Name = updated.Name;
        connection.ConnectionString = updated.ConnectionString;
        connection.SaveCredentials = updated.SaveCredentials;
        connection.Databases = [];

        var result = await TestConnectionAsync(connection.ConnectionString, connection.Type);
        ApplyConnectionResult(connection, result);

        await _connectionService.UpdateConnection(connection);
        OnConnectionStateChanged();

        return result;
    }

    public IDatabaseProvider GetProvider(DatabaseType type) => _providerFactory.GetProvider(type);
}