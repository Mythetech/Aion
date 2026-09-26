using Aion.Components.Connections;
using Aion.Components.History.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.History.Consumers;

public class HistoryEntryOpener : IConsumer<OpenHistoryEntry>
{
    private readonly QueryState _queries;
    private readonly ConnectionState _connections;
    private readonly IMessageBus _bus;

    public HistoryEntryOpener(QueryState queries, ConnectionState connections, IMessageBus bus)
    {
        _queries = queries;
        _connections = connections;
        _bus = bus;
    }

    public async Task Consume(OpenHistoryEntry message)
    {
        var entry = message.Entry;
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == entry.ConnectionId);

        // A new tab rather than the tab that ran it: that tab may have moved on, and its name is the
        // key desktop saves use, so reusing it could overwrite a saved query.
        var query = _queries.Clone(new QueryModel
        {
            Name = "Untitled",
            Query = entry.Sql,
            SavedQuery = "",
            ConnectionId = connection?.Id,
            DatabaseName = connection is null ? null : entry.DatabaseName
        });

        await _bus.PublishAsync(new FocusQuery(query));

        if (connection is null)
        {
            if (entry.ConnectionId is not null)
            {
                var name = string.IsNullOrWhiteSpace(entry.ConnectionName) ? "The connection" : $"Connection \"{entry.ConnectionName}\"";
                await _bus.PublishAsync(new AddNotification(
                    $"{name} no longer exists. Choose a connection to run this query.", Severity.Warning));
            }

            return;
        }

        if (message.Run)
        {
            await _bus.PublishAsync(new RunQuery());
        }
    }
}
