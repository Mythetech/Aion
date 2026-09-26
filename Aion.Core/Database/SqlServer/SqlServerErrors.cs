using Aion.Contracts.Queries;
using Microsoft.Data.SqlClient;

namespace Aion.Core.Database.SqlServer;

public static class SqlServerErrors
{
    public static QueryError ToQueryError(Exception exception, string sql)
    {
        if (exception is not SqlException sqlServer)
        {
            return QueryErrorNormalizer.Normalize(exception.Message, sql);
        }

        return QueryErrorNormalizer.Normalize(sqlServer.Message, sql, new EngineErrorDetails
        {
            Code = $"Msg {sqlServer.Number}"
        });
    }
}
