using Aion.Core.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionFinished(Guid ConnectionId, TransactionInfo Transaction, bool IsCommitted); 