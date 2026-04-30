using Aion.Contracts.Connections;
using Aion.Contracts.Database;

namespace Aion.Components.Connections.Commands;

public record ExpandTable(ConnectionModel Connection, DatabaseModel Database, string TableDisplayName);