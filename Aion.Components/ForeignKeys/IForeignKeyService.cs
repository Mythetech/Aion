namespace Aion.Components.ForeignKeys;

public interface IForeignKeyService
{
    Task<Dictionary<string, object>?> FetchReferencedRowAsync(
        Guid connectionId,
        string database,
        string referencedTable,
        string referencedColumn,
        object foreignKeyValue,
        CancellationToken cancellationToken = default);
}
