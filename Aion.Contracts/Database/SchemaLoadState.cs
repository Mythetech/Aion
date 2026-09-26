namespace Aion.Contracts.Database;

public enum SchemaLoadStatus
{
    NotLoaded,
    Loading,
    Loaded,
    Failed
}

/// <summary>
/// Where one lazily loaded part of a database's schema stands. A failure keeps the driver's message so the
/// schema tree can show it next to a retry instead of spinning.
/// </summary>
public sealed record SchemaLoadState(SchemaLoadStatus Status, string? Error = null)
{
    public static SchemaLoadState NotLoaded { get; } = new(SchemaLoadStatus.NotLoaded);

    public static SchemaLoadState Loading { get; } = new(SchemaLoadStatus.Loading);

    public static SchemaLoadState Loaded { get; } = new(SchemaLoadStatus.Loaded);

    public static SchemaLoadState Failed(string error) => new(SchemaLoadStatus.Failed, error);

    public bool IsLoaded => Status == SchemaLoadStatus.Loaded;

    public bool IsFailed => Status == SchemaLoadStatus.Failed;
}
