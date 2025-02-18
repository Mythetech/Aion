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
    
    public List<ConnectionModel> Connections { get; set; } = [];

    public async Task ConnectAsync(string connectionString)
    {
        try
        {
            var tables = await _connectionService.ConnectAsync(connectionString);

            var segments = connectionString.Split(';');
            Connections.Add(new ConnectionModel()
            {
                Name = segments[1].Replace("Database=", ""),
                ConnectionString = connectionString,
                Tables = tables
            });
            
            OnConnectionStateChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            /*
            _snackbar ??= _provider.GetSnackbar();
            _snackbar.AddAionNotification(ex.Message, Severity.Error);
            */
        }
    }
    
    public async Task<QueryResult> ExecuteQueryAsync(QueryModel query)
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

            var connection = Connections.First();
            Console.WriteLine($"Executing query on connection: {connection.Name}");
            Console.WriteLine($"Query text: {query.Query}");

            var result = await _connectionService.ExecuteQueryAsync(connection.ConnectionString, query.Query);
            
            query.IsExecuting = false;
            OnConnectionStateChanged();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Query execution failed: {ex.Message}");
            query.IsExecuting = false;
            OnConnectionStateChanged();
            return default;
        }
    }
    
}