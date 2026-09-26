using Aion.Components.Connections;
using Aion.Components.ForeignKeys.Commands;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.ForeignKeys.Consumers;

/// <summary>
/// Opens the rows a foreign key points at as an ordinary query tab, so they can be filtered, edited or followed
/// further like any other result.
/// </summary>
public class ForeignKeyRowsOpener : IConsumer<OpenForeignKeyRows>
{
    private readonly IForeignKeyService _foreignKeys;
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly IMessageBus _bus;

    public ForeignKeyRowsOpener(IForeignKeyService foreignKeys, ConnectionState connections, QueryState queries, IMessageBus bus)
    {
        _foreignKeys = foreignKeys;
        _connections = connections;
        _queries = queries;
        _bus = bus;
    }

    public async Task Consume(OpenForeignKeyRows message)
    {
        var detail = message.Detail;
        var lookup = _foreignKeys.BuildLookupQuery(detail);
        if (lookup.Sql is null)
        {
            await _bus.PublishAsync(new AddNotification(lookup.Error ?? "The related rows could not be opened.", Severity.Warning));
            return;
        }

        var query = _queries.AddQuery($"{detail.ReferencedTableDisplayName} - {detail.ReferencedColumn} {detail.ForeignKeyValue}");
        query.ConnectionId = detail.ConnectionId;
        query.DatabaseName = detail.DatabaseName;
        query.Query = lookup.Sql;

        // The related rows come from one table too, so their own foreign keys can be followed further.
        var schema = detail.ReferencedSchema ?? "";
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == detail.ConnectionId);
        if (connection != null
            && await _connections.GetTableColumnsAsync(connection, detail.DatabaseName, schema, detail.ReferencedTable) is { } columns)
        {
            query.EditMetadata = QueryEditMetadata.ForReadOnlyRows(connection.Id, detail.DatabaseName, schema, detail.ReferencedTable, columns);
        }

        // The editor loads a tab's text when it is told to focus it, and Run reads the editor, so the run
        // waits for the focus to finish.
        await _bus.PublishAsync(new FocusQuery(query));
        await _bus.PublishAsync(new RunQuery());
    }
}
