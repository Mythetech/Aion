namespace Aion.Contracts.Queries;

public readonly struct TransactionInfo
{
    public string Id { get; init; }
    public DateTime StartTime { get; init; }
    public TransactionStatus Status { get; init; }
    public int StatementCount { get; init; }

    /// <summary>
    /// The rows the transaction's statements inserted, updated or deleted, as far as the provider reported them.
    /// </summary>
    public int RowsChanged { get; init; }

    public TransactionInfo()
    {
        Id = Guid.NewGuid().ToString();
        StartTime = DateTime.UtcNow;
        Status = TransactionStatus.Active;
    }

    public TransactionInfo WithStatus(TransactionStatus newStatus) => this with { Status = newStatus };

    /// <param name="rowsAffected">The statement's <see cref="QueryResult.RowsAffected"/>; null counts as none.</param>
    public TransactionInfo WithStatementExecuted(int? rowsAffected = null) => this with
    {
        StatementCount = StatementCount + 1,
        RowsChanged = RowsChanged + (rowsAffected ?? 0)
    };
}

public enum TransactionStatus
{
    Active,
    Committed,
    RolledBack
}
