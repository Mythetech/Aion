using Aion.Core.Connections;
using Aion.Core.Database;

namespace Aion.Components.Connections.Commands;

public record ExpandTable(ConnectionModel Connection, DatabaseModel Database, string TableName);