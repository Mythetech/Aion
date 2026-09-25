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
using Aion.Components.Connections.Commands;

namespace Aion.Components.Connections;

public class ConnectionState
{
    private readonly IConnectionService _connectionService;
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ConnectionState> _logger;

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
            
            if (query.IncludeEstimatedPlan)
            {
                query.EstimatedPlan = await provider.GetEstimatedPlanAsync(connectionString, query.Query);
                await NotifyQueryChanged();
            }

            if (query.IncludeActualPlan)
            {
                query.ActualPlan = await provider.GetActualPlanAsync(connectionString, query.Query);
                var qr = new QueryResult { Error = "Query not executed - actual plan requested" };
                query.IsExecuting = false;
                await _messageBus.PublishAsync(new QueryExecuted(query));
                return qr;
            }

            if (query.UseTransaction)
            {
                await _messageBus.PublishAsync(new StartTransaction(query));
            }
            
            var result = query.Transaction?.Status == TransactionStatus.Active
                ? await provider.ExecuteInTransactionAsync(
                    connectionString,
                    query.Query,
                    query.Transaction.Value.Id,
                    cancellationToken)
                : await provider.ExecuteQueryAsync(
                    connectionString,
                    query.Query,
                    cancellationToken);

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

    public async Task<ConnectionResult> RefreshDatabaseAsync(ConnectionModel connection)
    {
        var result = await TestConnectionAsync(connection.ConnectionString, connection.Type);
        ApplyConnectionResult(connection, result);
        OnConnectionStateChanged();
        return result;
    }

    private static void ApplyConnectionResult(ConnectionModel connection, ConnectionResult result)
    {
        if (result.Success)
        {
            connection.Databases = result.Databases.Select(db => new DatabaseModel { Name = db }).ToList();
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