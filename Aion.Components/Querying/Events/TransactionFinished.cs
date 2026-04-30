using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionFinished(Guid ConnectionId, TransactionInfo Transaction, bool IsCommitted); 