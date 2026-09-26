using Aion.Components.RequestContextPanel;

namespace Aion.Components.ForeignKeys;

public interface IForeignKeyService
{
    /// <summary>
    /// Builds the SELECT that reads the rows a foreign key value points at, quoted and escaped for the
    /// connection's engine. Returns an error instead when the connection is gone or its engine has no SQL dialect.
    /// </summary>
    ForeignKeyLookupQuery BuildLookupQuery(ForeignKeyDetail detail, int? limit = null);

    /// <summary>
    /// Reads the row a foreign key value points at. A lookup that matches nothing has neither a row nor an error.
    /// </summary>
    Task<ForeignKeyLookup> FetchReferencedRowAsync(ForeignKeyDetail detail, CancellationToken cancellationToken = default);
}

public sealed record ForeignKeyLookupQuery(string? Sql, string? Error);

public sealed record ForeignKeyLookup(Dictionary<string, object>? Row, string? Error);
