using Aion.Contracts.Queries;
using Npgsql;

namespace Aion.Core.Database.PostgreSQL;

public static class PostgreSqlErrors
{
    public static QueryError ToQueryError(Exception exception, string sql)
    {
        if (exception is not PostgresException postgres)
        {
            return QueryErrorNormalizer.Normalize(exception.Message, sql);
        }

        return QueryErrorNormalizer.Normalize(postgres.Message, sql, new EngineErrorDetails
        {
            Message = postgres.MessageText,
            Code = $"SQLSTATE {postgres.SqlState}"
        });
    }
}
