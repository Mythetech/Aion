using Aion.Contracts.Connections;

namespace Aion.Components.Connections;

public static class DefaultDatabase
{
    /// <summary>
    /// The database a query tab should use once it is pointed at <paramref name="connection"/>: the
    /// connection's only database, or the one its connection string names. Otherwise there is no safe
    /// guess and the user chooses.
    /// </summary>
    public static string? For(ConnectionModel connection)
    {
        if (connection.Databases.Count == 1)
            return connection.Databases[0].Name;

        var named = ConnectionStringComposer.FindDatabase(connection.Type, connection.ConnectionString);
        if (named == null)
            return null;

        // The server's spelling is what the Database list shows and what later lookups match on.
        return connection.Databases.FirstOrDefault(d => d.Name.Equals(named, StringComparison.OrdinalIgnoreCase))?.Name
               ?? named;
    }
}
