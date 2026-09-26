using Aion.Components.Connections.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Database;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

/// <summary>
/// Opens CREATE statements as ordinary query tabs. The user edits the placeholders and runs the statement
/// themselves, and the schema tree refreshes when it succeeds.
/// </summary>
public class SchemaTemplateOpener : IConsumer<OpenCreateTableTemplate>, IConsumer<OpenCreateDatabaseTemplate>
{
    public const string NewDatabaseName = "new_database";

    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly IMessageBus _bus;

    public SchemaTemplateOpener(ConnectionState connections, QueryState queries, IMessageBus bus)
    {
        _connections = connections;
        _queries = queries;
        _bus = bus;
    }

    public async Task Consume(OpenCreateTableTemplate message)
    {
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == message.ConnectionId);
        if (connection == null) return;

        if (_connections.GetProvider(connection.Type) is not ISqlDialectProvider { Dialect: var dialect })
        {
            await _bus.PublishAsync(new AddNotification(
                $"Creating tables is not supported for {connection.Type} connections.", Severity.Warning));
            return;
        }

        await OpenAsync($"New table - {message.DatabaseName}", connection.Id, message.DatabaseName, dialect.CreateTableTemplate());
    }

    public async Task Consume(OpenCreateDatabaseTemplate message)
    {
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == message.ConnectionId);
        if (connection == null) return;

        var provider = _connections.GetProvider(connection.Type);
        if (provider is not IDatabaseCreationProvider)
        {
            await _bus.PublishAsync(new AddNotification(
                $"{connection.Name} holds a single database, so another one can't be added to it.", Severity.Warning));
            return;
        }

        // CREATE DATABASE runs from any database on the server; the tab needs one selected to run at all.
        var sql = await provider.Commands.GenerateCreateDatabaseScript(NewDatabaseName);
        await OpenAsync($"New database - {connection.Name}", connection.Id, connection.Databases.FirstOrDefault()?.Name, sql.Trim());
    }

    private async Task OpenAsync(string name, Guid connectionId, string? databaseName, string sql)
    {
        var query = _queries.AddQuery(name);
        query.ConnectionId = connectionId;
        query.DatabaseName = databaseName;
        query.Query = sql;

        await _bus.PublishAsync(new FocusQuery(query));
    }
}
