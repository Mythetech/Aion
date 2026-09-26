using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Events;

public record TransactionStarted(Guid ConnectionId, Guid QueryId, TransactionInfo Transaction);
