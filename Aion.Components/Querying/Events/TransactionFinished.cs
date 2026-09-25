using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionFinished(Guid ConnectionId, Guid QueryId, TransactionInfo Transaction, bool IsCommitted);
