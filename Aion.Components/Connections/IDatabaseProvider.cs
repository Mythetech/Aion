using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Core.Queries;

namespace Aion.Desktop.Database;

public interface IDatabaseProvider
{
    /// <summary>
    /// Gets a list of available databases from the server
    /// </summary>
    Task<List<string>> GetDatabasesAsync(string connectionString);
    
    /// <summary>
    /// Gets a list of tables for the specified database
    /// </summary>
    Task<List<string>> GetTablesAsync(string connectionString, string database);
    
    /// <summary>
    /// Executes a query and returns the results
    /// </summary>
    Task<QueryResult> ExecuteQueryAsync(string connectionString, string query, CancellationToken cancellationToken);
    
    /// <summary>
    /// Updates the connection string to use the specified database
    /// </summary>
    string UpdateConnectionString(string connectionString, string database);
    
    /// <summary>
    /// Gets the default port for this database type
    /// </summary>
    int GetDefaultPort();
    
    /// <summary>
    /// Validates a connection string for this database type
    /// </summary>
    bool ValidateConnectionString(string connectionString, out string? error);
} 