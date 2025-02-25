namespace Aion.Core.Queries;

public readonly struct TransactionInfo
{
    public string Id { get; init; }
    public DateTime StartTime { get; init; }
    public TransactionStatus Status { get; init; }

    public TransactionInfo()
    {
        Id = Guid.NewGuid().ToString();
        StartTime = DateTime.UtcNow;
        Status = TransactionStatus.Active;
    }

    public TransactionInfo WithStatus(TransactionStatus newStatus) => 
        new() { Id = Id, StartTime = StartTime, Status = newStatus };
}

public enum TransactionStatus
{
    Active,
    Committed,
    RolledBack
} 