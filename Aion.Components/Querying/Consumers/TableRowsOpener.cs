using Aion.Components.Connections;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Querying.Consumers;

public class TableRowsOpener : IConsumer<OpenTableRows>
{
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly IMessageBus _bus;

    public TableRowsOpener(ConnectionState connections, QueryState queries, IMessageBus bus)
    {
        _connections = connections;
        _queries = queries;
        _bus = bus;
    }

    public static string TabName(string tableDisplayName, int count) => $"First {count} rows - {tableDisplayName}";

    public async Task Consume(OpenTableRows message)
    {
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == message.ConnectionId);
        if (connection == null)
        {
            await _bus.PublishAsync(new AddNotification("The connection for this table no longer exists.", Severity.Warning));
            return;
        }

        var commands = _connections.GetProvider(connection.Type).Commands;
        var sql = await commands.GenerateSelectTopScript(message.DatabaseName, message.Schema, message.TableName, message.Count);

        var displayName = string.IsNullOrEmpty(message.Schema) ? message.TableName : $"{message.Schema}.{message.TableName}";
        var query = _queries.AddQuery(TabName(displayName, message.Count));
        query.ConnectionId = connection.Id;
        query.DatabaseName = message.DatabaseName;
        query.Query = sql.Trim();

        // The editor loads a tab's text when it is told to focus it, and Run reads the editor, so the run
        // waits for the focus to finish.
        await _bus.PublishAsync(new FocusQuery(query));
        await _bus.PublishAsync(new RunQuery());
    }
}
