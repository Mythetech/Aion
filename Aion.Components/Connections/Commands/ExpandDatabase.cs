using Aion.Contracts.Connections;

namespace Aion.Components.Connections.Commands;

public record ExpandDatabase(ConnectionModel Connection, string DatabaseName);