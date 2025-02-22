using MudBlazor;

namespace Aion.Components.Connections.Models;

public class DatabaseTreeItem : TreeItemData<string>
{
    public string ItemType { get; set; } // "database", "table", "column", "index", etc.
    public string Schema { get; set; } = "public"; // For PostgreSQL/SQL Server
    public string DataType { get; set; } // For columns
    public bool IsNullable { get; set; } // For columns
    public string DefaultValue { get; set; } // For columns
    public bool IsPrimaryKey { get; set; } // For columns/indexes
    public string ConnectionId { get; set; }
    public string DatabaseName { get; set; }
    public string TableName { get; set; }
} 