using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using NSubstitute;

namespace Aion.Test.TestDoubles;

/// <summary>
/// Substitute SQLite and PGlite providers that accept every statement and bind each connection to the
/// database named in its connection string, the way the in-browser providers do.
/// </summary>
public sealed class InBrowserEngines
{
    public IDatabaseProvider Sqlite { get; }
    public IDatabaseProvider Postgres { get; }
    public IDatabaseProviderFactory Factory { get; }
    public IConnectionService ConnectionService { get; } = Substitute.For<IConnectionService>();

    public InBrowserEngines()
    {
        Sqlite = Create(DatabaseType.WasmSQLite, cs => cs.Split(';')[0].Replace("Data Source=", ""));
        Postgres = Create(DatabaseType.WasmPostgreSQL, cs => cs.Replace("pglite://", ""));
        Factory = new DatabaseProviderFactory([Sqlite, Postgres]);

        ConnectionService.GetSavedConnections().Returns(Enumerable.Empty<ConnectionModel>());
        ConnectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns(ci => Factory.GetProvider(ci.ArgAt<DatabaseType>(1)).GetDatabasesAsync(ci.ArgAt<string>(0)));
    }

    public IDatabaseProvider For(DatabaseType engine) => engine == DatabaseType.WasmSQLite ? Sqlite : Postgres;

    public IDatabaseProvider Other(DatabaseType engine) => engine == DatabaseType.WasmSQLite ? Postgres : Sqlite;

    private static IDatabaseProvider Create(DatabaseType type, Func<string, string> databaseName)
    {
        var provider = Substitute.For<IDatabaseProvider, IManagedDatabaseProvider>();
        provider.DatabaseType.Returns(type);
        provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new QueryResult()));
        provider.GetDatabasesAsync(Arg.Any<string>())
            .Returns(ci => Task.FromResult<List<string>?>([databaseName(ci.ArgAt<string>(0))]));
        return provider;
    }
}
