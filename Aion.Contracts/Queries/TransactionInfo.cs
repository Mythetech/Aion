namespace Aion.Contracts.Queries;

public readonly struct TransactionInfo
{
    public string Id { get; init; }
    public DateTime StartTime { get; init; }
    public TransactionStatus Status { get; init; }
    public int StatementCount { get; init; }

    public TransactionInfo()
    {
        Id = Guid.NewGuid().ToString();
        StartTime = DateTime.UtcNow;
        Status = TransactionStatus.Active;
    }

    public TransactionInfo WithStatus(TransactionStatus newStatus) => this with { Status = newStatus };

    public TransactionInfo WithStatementExecuted() => this with { StatementCount = StatementCount + 1 };
}

public enum TransactionStatus
{
    Active,
    Committed,
    RolledBack
}
