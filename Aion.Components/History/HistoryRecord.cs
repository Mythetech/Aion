using System.Text.Json;

namespace Aion.Components.History;

public record HistoryRecord(DateTimeOffset Timestamp, string Json);

public record HistoryRecord<T>(T Data) : HistoryRecord(DateTimeOffset.Now, JsonSerializer.Serialize(Data));