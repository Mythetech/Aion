using System.Text;
using System.Text.Json;
using Aion.Contracts.Queries;

namespace Aion.Web.Providers;

/// <summary>
/// Maps the error object pglite-interop.js returns for a failed statement, which carries the fields of
/// PGlite's DatabaseError (message, SQLSTATE code, position, detail, hint).
/// </summary>
public static class PGliteErrors
{
    public static QueryError ToQueryError(JsonElement error, string sql)
    {
        var message = ReadString(error, "message") ?? "Unknown PGlite error";
        var code = ReadString(error, "code");
        var detail = ReadString(error, "detail");
        var hint = ReadString(error, "hint");

        return QueryErrorNormalizer.Normalize(BuildRaw(message, code, detail, hint), sql, new EngineErrorDetails
        {
            Message = message,
            Code = code == null ? null : $"SQLSTATE {code}",
            Position = int.TryParse(ReadString(error, "position"), out var position) ? position : null
        });
    }

    // Shaped like Npgsql's PostgresException.Message so a copied error reads the same on web and desktop.
    private static string BuildRaw(string message, string? code, string? detail, string? hint)
    {
        var raw = new StringBuilder(code == null ? message : $"{code}: {message}");
        if (detail != null) raw.Append($"\n\nDETAIL: {detail}");
        if (hint != null) raw.Append($"\nHINT: {hint}");
        return raw.ToString();
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
