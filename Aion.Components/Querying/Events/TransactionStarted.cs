using Aion.Core.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionStarted(Guid ConnectionId, TransactionInfo Transaction); 