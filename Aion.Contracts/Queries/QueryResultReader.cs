using System.Data.Common;

namespace Aion.Contracts.Queries;

/// <summary>
/// Reads a data reader's current result set into a <see cref="QueryResult"/> the same way for every ADO.NET provider.
/// </summary>
public static class QueryResultReader
{
    public static async Task ReadAsync(DbDataReader reader, QueryResult result, CancellationToken cancellationToken)
    {
        var fieldCount = reader.FieldCount;
        for (var i = 0; i < fieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
            result.ColumnTypes.Add(DataTypeName(reader, i));
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object>(fieldCount);
            for (var i = 0; i < fieldCount; i++)
            {
                var value = reader.GetValue(i);
                row[result.Columns[i]] = value == DBNull.Value ? null! : value;
            }

            result.Rows.Add(row);
        }
    }

    // The type only labels the column, so a driver that cannot name it (the in-browser SQLite reader has no
    // types for a result without rows) leaves the label off rather than failing the query.
    private static string? DataTypeName(DbDataReader reader, int ordinal)
    {
        try
        {
            var name = reader.GetDataTypeName(ordinal);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
