using Aion.Core.Connections;

namespace Aion.Components.Connections.Commands;

public record ExpandDatabase(ConnectionModel Connection, string DatabaseName);