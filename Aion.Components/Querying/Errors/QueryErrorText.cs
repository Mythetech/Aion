using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Errors;

public static class QueryErrorText
{
    public static string? Location(QueryError error) => error switch
    {
        { Line: { } line, Column: { } column } => $"Line {line}, column {column}",
        { Line: { } line } => $"Line {line}",
        _ => null
    };

    public static string LogLine(QueryError error) =>
        Location(error) is { } location
            ? $"Error at {char.ToLowerInvariant(location[0])}{location[1..]}: {error.Message}"
            : $"Error: {error.Message}";
}
