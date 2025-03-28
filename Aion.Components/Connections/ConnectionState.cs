using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Components.Shared.Snackbar;
using Aion.Core.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;
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
    
    public List<ConnectionModel> Connections { get; set; } = [new ConnectionModel()
    {
        Name = "TestDefault",
        Active = false,
    }];

    public async Task InitializeAsync()
    {
        await _connectionService.InitializeAsync();
        var savedConnections = await _connectionService.GetSavedConnections();
        Connections = savedConnections.ToList();
       
        foreach (var connection in Connections)
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
            
            connection.Databases = databases.Select(db => new DatabaseModel { Name = db }).ToList();
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
            _logger.LogError(ex, $"Failed to refresh databases for connection {connection.Name}: {ex.Message}");
        }
    }

    public async Task LoadColumnsAsync(ConnectionModel connection, DatabaseModel database, string table)
    {
        if (database.LoadedColumnTables.Contains(table)) return;

        try
        {
            var connectionString = connection.ConnectionString;
            var provider = GetProvider(connection.Type);
            connectionString = provider.UpdateConnectionString(connectionString, database.Name);
            
            var columns = await provider.GetColumnsAsync(connectionString, database.Name, table);
            database.TableColumns[table] = columns;
            database.LoadedColumnTables.Add(table);
            
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load columns for table {table}: {ex.Message}");
            throw;
        }
    }

    public IDatabaseProvider GetProvider(DatabaseType type) => _providerFactory.GetProvider(type);
}