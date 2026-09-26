using System.Text.RegularExpressions;
using Aion.Contracts.Queries;
using LiteDB;

namespace Aion.Core.Database.LiteDB;

public static partial class LiteDBErrors
{
    public static QueryError ToQueryError(Exception exception, string sql)
    {
        if (exception is not LiteException lite)
        {
            return QueryErrorNormalizer.Normalize(exception.Message, sql);
        }

        return QueryErrorNormalizer.Normalize(lite.Message, sql, new EngineErrorDetails
        {
            Code = $"Error {lite.ErrorCode}",
            Position = UnexpectedTokenIndex(lite, sql) is { } index ? CharacterPosition(sql, index) : null
        });
    }

    /// <summary>
    /// Where the token LiteDB could not parse starts, as an index into <paramref name="sql"/>. LiteDB measures its
    /// position back from where it stopped reading, which lands on the token's 0-based start for punctuation and
    /// its 1-based start for a word or number, so the token its message quotes decides which of the two it is.
    /// </summary>
    private static int? UnexpectedTokenIndex(LiteException lite, string sql)
    {
        var match = UnexpectedToken().Match(lite.Message);
        if (!match.Success || lite.Position <= 0)
        {
            return null;
        }

        var token = match.Groups["token"].Value;
        if (token == "[EOF]")
        {
            return sql.Length;
        }

        var position = (int)lite.Position;
        foreach (var index in new[] { position - 1, position })
        {
            if (index + token.Length <= sql.Length && string.CompareOrdinal(sql, index, token, 0, token.Length) == 0)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// LiteDB counts UTF-16 units, but <see cref="EngineErrorDetails.Position"/> counts characters the way
    /// PostgreSQL does, so a character outside the Basic Multilingual Plane before the error counts once.
    /// </summary>
    private static int CharacterPosition(string sql, int index)
    {
        var characters = 0;
        for (var i = 0; i < index; i++)
        {
            if (!(char.IsLowSurrogate(sql[i]) && i > 0 && char.IsHighSurrogate(sql[i - 1])))
            {
                characters++;
            }
        }

        return characters + 1;
    }

    [GeneratedRegex(@"^Unexpected token `(?<token>[^`]+)` in position \d+")]
    private static partial Regex UnexpectedToken();
}
