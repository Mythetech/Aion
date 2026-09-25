using Aion.Components.Connections;
using Aion.Components.Querying.Editing;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Aion.Contracts.Queries.Editing;
using Aion.Core.Database.PostgreSQL;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;

namespace Aion.Test.TestDoubles;

/// <summary>
/// A connection backed by a substitute PostgreSQL provider that uses the real PostgreSQL commands, so tests see
/// the exact SQL that would run and control what each execution reports.
/// </summary>
public sealed class EditingFixture
{
    public const string DatabaseName = "app";

    public IMessageBus Bus { get; } = Substitute.For<IMessageBus>();
    public IDatabaseProvider Provider { get; }
    public ConnectionState ConnectionState { get; }
    public ConnectionModel Connection { get; }
    public DatabaseModel Database { get; }
    public List<string> ExecutedSql { get; } = [];

    private readonly Queue<QueryResult> _results = new();

    public EditingFixture(bool supportsEditing = true)
    {
        Provider = supportsEditing
            ? Substitute.For<IDatabaseProvider, IDatabaseRowEditingProvider>()
            : Substitute.For<IDatabaseProvider>();
        Provider.Commands.Returns(new PostgreSqlCommands());
        Provider.DatabaseType.Returns(DatabaseType.PostgreSQL);
        Provider.UpdateConnectionString(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => $"{ci.ArgAt<string>(0)};Database={ci.ArgAt<string>(1)}");
        Provider.BeginTransactionAsync(Arg.Any<string>()).Returns(_ => new TransactionInfo());
        Provider.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Execute(ci.ArgAt<string>(1)));
        Provider.ExecuteInTransactionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Execute(ci.ArgAt<string>(1)));

        var factory = Substitute.For<IDatabaseProviderFactory>();
        factory.GetProvider(DatabaseType.PostgreSQL).Returns(Provider);

        ConnectionState = new ConnectionState(
            Substitute.For<IConnectionService>(), factory, Bus, NullLogger<ConnectionState>.Instance);

        Database = new DatabaseModel { Name = DatabaseName };
        Connection = new ConnectionModel
        {
            Name = "test",
            ConnectionString = "Host=db",
            Type = DatabaseType.PostgreSQL,
            Databases = [Database]
        };
        ConnectionState.Connections = [Connection];
    }

    public static List<ColumnInfo> UserColumns() =>
    [
        new ColumnInfo { Name = "id", IsPrimaryKey = true },
        new ColumnInfo { Name = "name" }
    ];

    public PendingChangesSqlBuilder CreateSqlBuilder() => new(ConnectionState, new SqlChangeGenerator());

    public EditableQueryResult CreateEditableResult() => new()
    {
        Columns = ["id", "name"],
        Rows =
        [
            new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada" },
            new Dictionary<string, object> { ["id"] = 2, ["name"] = "Grace" }
        ],
        SourceTable = "users",
        SourceSchema = "public",
        SourceDatabase = DatabaseName,
        ConnectionId = Connection.Id,
        ColumnMetadata = UserColumns()
    };

    /// <summary>
    /// Queues what the next execution reports; executions beyond the queue report one affected row.
    /// </summary>
    public void EnqueueResult(QueryResult result) => _results.Enqueue(result);

    public List<object> Published() => Bus.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IMessageBus.PublishAsync))
        .Select(c => c.GetArguments()[0]!)
        .ToList();

    public List<AddNotification> Notifications() => Published().OfType<AddNotification>().ToList();

    private QueryResult Execute(string sql)
    {
        ExecutedSql.Add(sql);
        return _results.Count > 0 ? _results.Dequeue() : new QueryResult { RowsAffected = 1 };
    }
}
