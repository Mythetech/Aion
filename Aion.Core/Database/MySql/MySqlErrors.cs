using Aion.Contracts.Queries;
using MySql.Data.MySqlClient;

namespace Aion.Core.Database.MySql;

public static class MySqlErrors
{
    public static QueryError ToQueryError(Exception exception, string sql, string? rawOverride = null)
    {
        var raw = rawOverride ?? exception.Message;
        if (exception is not MySqlException mySql || mySql.Number == 0)
        {
            return QueryErrorNormalizer.Normalize(raw, sql);
        }

        return QueryErrorNormalizer.Normalize(raw, sql, new EngineErrorDetails
        {
            Code = $"Error {mySql.Number}"
        });
    }
}
