using Aion.Core.Database;

namespace Aion.Components.Connections;

public class ConnectionDialogModel
{
    public string? Name { get; set; } = null;
    public DatabaseType Type { get; set; } = DatabaseType.PostgreSQL;
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    
    public bool SaveCredentials { get; set; }
} 