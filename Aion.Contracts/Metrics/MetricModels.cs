namespace Aion.Contracts.Metrics;

public record DatabaseSizeInfo(string DatabaseName, long SizeBytes, int TableCount);
public record TableSizeInfo(string TableName, long SizeBytes, long RowCount);
public record SlowQueryInfo(string Query, TimeSpan Duration, DateTime StartedAt, string? Username);
public record ActiveQueryInfo(string Query, TimeSpan ElapsedTime, string? Username, string? State);
public record ConnectionPoolSnapshot(int ActiveConnections, int MaxConnections);
public record ServerHealthSnapshot(TimeSpan? Uptime, string? Version);
public record StorageSnapshot(DatabaseSizeInfo? DatabaseSize, IReadOnlyList<TableSizeInfo>? TableSizes);
public record PerformanceSnapshot(double? CacheHitRatio, IReadOnlyList<ActiveQueryInfo>? ActiveQueries);
