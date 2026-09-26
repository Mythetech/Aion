using Aion.Components.Connections;
using Aion.Components.RequestContextPanel;
using Aion.Contracts.Database;

namespace Aion.Components.ForeignKeys;

public class ForeignKeyService : IForeignKeyService
{
    private readonly ConnectionState _connectionState;

    public ForeignKeyService(ConnectionState connectionState)
    {
        _connectionState = connectionState;
    }

    public ForeignKeyLookupQuery BuildLookupQuery(ForeignKeyDetail detail, int? limit = null)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == detail.ConnectionId);
        if (connection == null)
            return new ForeignKeyLookupQuery(null, "The connection for this result no longer exists.");

        if (_connectionState.GetProvider(connection.Type) is not ISqlDialectProvider { Dialect: var dialect })
            return new ForeignKeyLookupQuery(null, $"Following foreign keys is not supported for {connection.Type} connections.");

        var predicate = dialect.BuildKeyPredicate([new ColumnValue(detail.ReferencedColumn, detail.ForeignKeyValue)]);
        var sql = dialect.SelectRows(dialect.QualifyTable(detail.ReferencedSchema, detail.ReferencedTable), predicate, limit);
        return new ForeignKeyLookupQuery(sql, null);
    }

    public async Task<ForeignKeyLookup> FetchReferencedRowAsync(ForeignKeyDetail detail, CancellationToken cancellationToken = default)
    {
        var query = BuildLookupQuery(detail, limit: 1);
        if (query.Sql is null)
            return new ForeignKeyLookup(null, query.Error);

        var connection = _connectionState.Connections.First(c => c.Id == detail.ConnectionId);
        var provider = _connectionState.GetProvider(connection.Type);
        var connectionString = provider.UpdateConnectionString(connection.ConnectionString, detail.DatabaseName);

        var result = await provider.ExecuteQueryAsync(connectionString, query.Sql, cancellationToken);

        return result.Success
            ? new ForeignKeyLookup(result.Rows.FirstOrDefault(), null)
            : new ForeignKeyLookup(null, result.Error);
    }
}
