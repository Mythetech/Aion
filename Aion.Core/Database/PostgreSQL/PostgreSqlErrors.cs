using Aion.Contracts.Queries;
using Npgsql;

namespace Aion.Core.Database.PostgreSQL;

public static class PostgreSqlErrors
{
    /// <param name="command">The command that ran <paramref name="sql"/>, which tells which of its statements failed.</param>
    public static QueryError ToQueryError(Exception exception, string sql, NpgsqlCommand? command = null)
    {
        if (exception is not PostgresException postgres)
        {
            return QueryErrorNormalizer.Normalize(exception.Message, sql);
        }

        var statementOffset = postgres.Position > 0 ? FailedStatementOffset(postgres, sql, command) : null;
        return QueryErrorNormalizer.Normalize(postgres.Message, sql, new EngineErrorDetails
        {
            Message = postgres.MessageText,
            Code = $"SQLSTATE {postgres.SqlState}",
            Position = statementOffset == null ? null : postgres.Position,
            StatementOffset = statementOffset ?? 0
        });
    }

    /// <summary>
    /// Npgsql sends a multi-statement command as separate statements, and PostgreSQL measures the
    /// position from the start of the one that failed. Null when that start can't be told, in which
    /// case the error is located from its message instead.
    /// </summary>
    private static int? FailedStatementOffset(PostgresException postgres, string sql, NpgsqlCommand? command)
    {
        // Statements is obsolete in favour of NpgsqlBatch, but it is the only public way to learn which
        // statement of a plain multi-statement command failed.
#pragma warning disable CS0618
        var statements = command?.Statements;
#pragma warning restore CS0618
        var index = statements == null || postgres.BatchCommand == null ? -1 : IndexOf(statements, postgres.BatchCommand);

        if (index >= 0) return PostgreSqlStatementStarts.StartOf(sql, index);

        return PostgreSqlStatementStarts.StartOf(sql, 1) == null ? 0 : null;
    }

    private static int IndexOf(IReadOnlyList<NpgsqlBatchCommand> statements, NpgsqlBatchCommand failed)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            if (ReferenceEquals(statements[i], failed)) return i;
        }

        return -1;
    }
}
