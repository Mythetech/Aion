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

        foreach (var connection in Connections.ToList())
        {
            await RefreshDatabaseAsync(connection);
        }

        OnConnectionStateChanged();
    }

    public async Task ConnectAsync(ConnectionModel connection)
    {
        try
        {
            var databases = await _connectionService.GetDatabasesAsync(connection.ConnectionString, connection.Type);

            connection.Databases = databases?.Select(db => new DatabaseModel { Name = db }).ToList() ?? [];
            connection.Active = true;
            
            await _connectionService.AddConnection(connection);
            
            Connections.Add(connection);
            
            await _messageBus.PublishAsync(new AddNotification($"Connected to {connection.Name}", Severity.Success));
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            await _messageBus.PublishAsync(new AddNotification($"Error connecting to {connection.Name}{Environment.NewLine}{ex.Message}", Severity.Error));

        }
    }

    public async Task LoadTablesAsync(ConnectionModel connection, DatabaseModel database)
    {
        if (database.TablesLoaded) return;

        try
        {
            var connectionString = connection.ConnectionString;
            var provider = GetProvider(connection.Type);
            connectionString = provider.UpdateConnectionString(connectionString, database.Name);

            var tables = await _connectionService.GetTablesAsync(connectionString, database.Name, connection.Type);
            database.Tables = tables;
            database.TablesLoaded = true;

            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load tables: {ex.Message}");
        }
    }
    
    public const string ActualPlanNotice =
        "Actual plan captured. The statement ran inside a transaction that was rolled back, so none of its changes were kept.";

    public const string ActualPlanInTransactionMessage =
        "Actual query plans can't be captured while this tab has an open transaction. Commit or roll back first, or turn off Actual Query Plan.";

    public async Task<QueryResult> ExecuteQueryAsync(QueryModel query, CancellationToken cancellationToken)
    {
        var connection = Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
        if (connection == null) return new QueryResult { Error = "Connection not found" };

        var provider = GetProvider(connection.Type);
        var connectionString = provider.UpdateConnectionString(connection.ConnectionString, query.DatabaseName);

        try 
        {
            query.StartExecution();
            await NotifyQueryChanged();
            
            if (query.IncludeEstimatedPlan && provider is IEstimatedQueryPlanProvider estimatedPlans)
            {
                query.EstimatedPlan = await GetEstimatedPlanAsync(estimatedPlans, connectionString, query.Query, cancellationToken);
                await NotifyQueryChanged();
            }

            if (query.IncludeActualPlan && provider is IActualQueryPlanProvider actualPlans)
            {
                return await CaptureActualPlanAsync(query, actualPlans, connectionString, cancellationToken);
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
                result = await provider.ExecuteInTransactionAsync(connectionString, query.Query, transaction.Id, cancellationToken);
                if (result.Success)
                {
                    query.Transaction = transaction.WithStatementExecuted();
                }
            }
            else
            {
                result = await provider.ExecuteQueryAsync(connectionString, query.Query, cancellationToken);
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
        QueryModel query, IActualQueryPlanProvider plans, string connectionString, CancellationToken cancellationToken)
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
            query.ActualPlan = await plans.GetActualPlanAsync(connectionString, query.Query, cancellationToken);
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

    public async Task RefreshDatabaseAsync(ConnectionModel connection)
    {
        try 
        {
            var databases = await _connectionService.GetDatabasesAsync(
                connection.ConnectionString, 
                connection.Type
            );
            if (databases != null)
            {
                connection.Databases = databases.Select(db => new DatabaseModel { Name = db }).ToList();
                connection.Active = true;
            }
            else
            {
                connection.Active = false;
            }
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to refresh databases for connection {connection.Name}: {ex.Message}");
        }
    }

    public bool SupportsIndexes(DatabaseType type) => _providerFactory.GetProvider(type) is IDatabaseIndexProvider;

    public bool SupportsRoutines(DatabaseType type) => _providerFactory.GetProvider(type) is IDatabaseRoutineProvider;

    public bool SupportsEstimatedPlan(DatabaseType type) => _providerFactory.GetProvider(type) is IEstimatedQueryPlanProvider;

    public bool SupportsActualPlan(DatabaseType type) => _providerFactory.GetProvider(type) is IActualQueryPlanProvider;

    public async Task LoadIndexesAsync(ConnectionModel connection, DatabaseModel database)
    {
        if (database.IndexesLoaded) return;

        var provider = GetProvider(connection.Type);
        if (provider is not IDatabaseIndexProvider indexProvider)
        {
            // Mark loaded so the UI doesn't spin forever on a provider that doesn't support indexes.
            database.IndexesLoaded = true;
            OnConnectionStateChanged();
            return;
        }

        try
        {
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database.Name);
            database.Indexes = await indexProvider.GetIndexesAsync(connectionString, database.Name);
            database.IndexesLoaded = true;
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load indexes for database {Database}", database.Name);
        }
    }

    public async Task LoadRoutinesAsync(ConnectionModel connection, DatabaseModel database)
    {
        if (database.RoutinesLoaded) return;

        var provider = GetProvider(connection.Type);
        if (provider is not IDatabaseRoutineProvider routineProvider)
        {
            database.RoutinesLoaded = true;
            OnConnectionStateChanged();
            return;
        }

        try
        {
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, database.Name);
            database.Routines = await routineProvider.GetRoutinesAsync(connectionString, database.Name);
            database.RoutinesLoaded = true;
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load routines for database {Database}", database.Name);
        }
    }

    public async Task LoadColumnsAsync(ConnectionModel connection, DatabaseModel database, string schema, string table)
    {
        var key = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        if (database.LoadedColumnTables.Contains(key)) return;

        try
        {
            var connectionString = connection.ConnectionString;
            var provider = GetProvider(connection.Type);
            connectionString = provider.UpdateConnectionString(connectionString, database.Name);

            var columns = await provider.GetColumnsAsync(connectionString, database.Name, schema, table);
            database.TableColumns[key] = columns;
            database.LoadedColumnTables.Add(key);

            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load columns for table {key}: {ex.Message}");
            throw;
        }
    }

    public async Task RemoveConnection(Guid id)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == id);
        if (connection == null) return;

        Connections.Remove(connection);
        await _connectionService.RemoveConnection(id);
        OnConnectionStateChanged();
    }

    public async Task UpdateConnection(Guid id, ConnectionModel updated)
    {
        var connection = Connections.FirstOrDefault(c => c.Id == id);
        if (connection == null) return;

        connection.Name = updated.Name;
        connection.ConnectionString = updated.ConnectionString;
        connection.SaveCredentials = updated.SaveCredentials;

        // Disconnect and reconnect with new settings
        connection.Active = false;
        connection.Databases = [];

        try
        {
            var provider = _providerFactory.GetProvider(connection.Type);
            var databases = await provider.GetDatabasesAsync(connection.ConnectionString);
            if (databases != null)
            {
                connection.Databases = databases.Select(db => new DatabaseModel { Name = db }).ToList();
                connection.Active = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reconnect after updating connection {Name}", connection.Name);
            await _messageBus.PublishAsync(new AddNotification(
                $"Updated {connection.Name} but failed to reconnect: {ex.Message}", Severity.Warning));
        }

        await _connectionService.UpdateConnection(connection);
        OnConnectionStateChanged();
    }

    public IDatabaseProvider GetProvider(DatabaseType type) => _providerFactory.GetProvider(type);
}