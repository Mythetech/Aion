using Aion.Components.Querying;
using Aion.Components.Shared.Snackbar;
using Aion.Core.Database;
using Aion.Core.Queries;
using MudBlazor;

namespace Aion.Components.Connections;

public class ConnectionState
{
    private readonly IConnectionService _connectionService;
    private readonly ISnackbarProvider _provider;
    private readonly IDatabaseProviderFactory _providerFactory;
    private ISnackbar? _snackbar;

    public ConnectionState(IConnectionService connectionService, IDatabaseProviderFactory providerFactory)
    {
        _connectionService = connectionService;
        _providerFactory = providerFactory;
    }
    
    public event Action? ConnectionStateChanged;
    
    protected void OnConnectionStateChanged() => ConnectionStateChanged?.Invoke();
    
    public List<ConnectionModel> Connections { get; set; } = [new ConnectionModel()
    {
        Name = "TestDefault",
        Active = false,
    }];

    public async Task ConnectAsync(ConnectionModel connection)
    {
        try
        {
            var databases = await _connectionService.GetDatabasesAsync(connection.ConnectionString, connection.Type);
            
            connection.Databases = databases.Select(db => new DatabaseModel { Name = db }).ToList();
            connection.Active = true;
            
            Connections.Add(connection);
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
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
            throw;
        }
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(QueryModel query, CancellationToken cancellationToken)
    {
        if (!Connections.Any())
        {
            Console.WriteLine("No connections available");
            return default;
        }
        
        try
        {
            query.IsExecuting = true;
            OnConnectionStateChanged();

            var connection = Connections.FirstOrDefault(x => x.Id == query.ConnectionId);
            if (connection == null)
            {
                throw new Exception("Selected connection not found");
            }

            var provider = GetProvider(connection.Type);
            var connectionString = provider.UpdateConnectionString(connection.ConnectionString, query.DatabaseName);
            
            var result = await _connectionService.ExecuteQueryAsync(connectionString, query.Query, connection.Type, cancellationToken);
            
            query.IsExecuting = false;
            OnConnectionStateChanged();
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Query execution cancelled");
            query.IsExecuting = false;
            OnConnectionStateChanged();
            return new QueryResult { Error = "Query cancelled" };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Query execution failed: {ex.Message}");
            query.IsExecuting = false;
            OnConnectionStateChanged();
            return new QueryResult { Error = ex.Message };
        }
    }

    private IDatabaseProvider GetProvider(DatabaseType type) => _providerFactory.GetProvider(type);
}