using System.Collections.Concurrent;

namespace Aion.Components.Connections;

/// <summary>Where a schema change was made, so the schema can be loaded again there.</summary>
public record PendingSchemaChange(Guid ConnectionId, string? DatabaseName, bool DatabasesChanged);

/// <summary>
/// Schema changes made inside transactions that are still open. Until a transaction commits its changes
/// are only visible to the tab that made them, and a rollback undoes them, so the schema tree waits.
/// </summary>
public class PendingSchemaChanges
{
    private readonly ConcurrentDictionary<string, PendingSchemaChange> _byTransaction = new();

    /// <summary>A transaction runs against one database, so later changes only widen what to reload.</summary>
    public void Remember(string transactionId, PendingSchemaChange change) =>
        _byTransaction.AddOrUpdate(transactionId, change,
            (_, earlier) => earlier with { DatabasesChanged = earlier.DatabasesChanged || change.DatabasesChanged });

    /// <summary>Forgets the transaction's changes, returning them, or null when it made none.</summary>
    public PendingSchemaChange? Take(string transactionId) =>
        _byTransaction.TryRemove(transactionId, out var change) ? change : null;
}
