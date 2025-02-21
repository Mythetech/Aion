using Aion.Core.Connections;

namespace Aion.Components.Connections.Events;

public record DatabaseCreated(ConnectionModel Connection, string DatabaseName); 