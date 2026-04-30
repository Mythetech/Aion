using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionStarted(Guid ConnectionId, TransactionInfo Transaction); 