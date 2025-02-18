using Aion.Components.Querying;
using Aion.Components.Shared.Snackbar;
using MudBlazor;

namespace Aion.Components.Connections;

public class ConnectionState
{
    private readonly IConnectionService _connectionService;
    private readonly ISnackbarProvider _provider;
    private ISnackbar? _snackbar;

    public ConnectionState(IConnectionService connectionService)
    {
        _connectionService = connectionService;
    }
    
    public event Action? ConnectionStateChanged;
    
    protected void OnConnectionStateChanged() => ConnectionStateChanged?.Invoke();
    
    public List<ConnectionModel> Connections { get; set; } = [new ConnectionModel()
    {
        Name = "TestDefault",
        Active = false,
    }];

    public async Task ConnectAsync(string connectionString)
    {
        try
        {
            var databases = await _connectionService.GetDatabasesAsync(connectionString);
            
            var segments = connectionString.Split(';');
            var host = segments.FirstOrDefault(s => s.StartsWith("Host="))?.Replace("Host=", "");
            
            Connections.Add(new ConnectionModel()
            {
                Name = host ?? "New Connection",
                ConnectionString = connectionString,
                Databases = databases.Select(db => new DatabaseModel { Name = db }).ToList(),
                Active = true,
            });
            
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public async Task LoadTablesAsync(ConnectionModel connection, DatabaseModel database)
    {
        if (database.TablesLoaded) return;

        try
        {
            var connectionString = connection.ConnectionString;
            // Ensure we're using the correct database
            connectionString = UpdateConnectionString(connectionString, database.Name);
            
            var tables = await _connectionService.GetTablesAsync(connectionString, database.Name);
            database.Tables = tables;
            database.TablesLoaded = true;
            
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load tables: {ex.Message}");
        }
    }

    private string UpdateConnectionString(string connectionString, string database)
    {
        var segments = connectionString.Split(';')
            .Where(s => !s.StartsWith("Database="))
            .ToList();
        segments.Add($"Database={database}");
        return string.Join(";", segments);
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

            var connectionString = UpdateConnectionString(connection.ConnectionString, query.DatabaseName);
            Console.WriteLine($"Executing query on connection: {connection.Name}, database: {query.DatabaseName}");
            Console.WriteLine($"Query text: {query.Query}");

            var result = await _connectionService.ExecuteQueryAsync(connectionString, query.Query, cancellationToken);
            
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
    
}