namespace Aion.Components.RequestContextPanel;

public record ForeignKeyDetail(
    string QueryName,
    string SourceColumn,
    string ReferencedTable,
    string ReferencedColumn,
    object ForeignKeyValue,
    Guid ConnectionId,
    string DatabaseName
);
