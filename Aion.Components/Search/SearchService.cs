using Aion.Components.AppContextPanel.Commands;
using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Theme;
using Aion.Core.Connections;
using Aion.Core.Database;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Search;

public class SearchService
{
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly ILogger<SearchService> _logger;
    private readonly IMessageBus _bus;

    public SearchService(ConnectionState connections, QueryState queries, ILogger<SearchService> logger, IMessageBus bus)
    {
        _connections = connections;
        _queries = queries;
        _logger = logger;
        _bus = bus;
    }

    public async IAsyncEnumerable<SearchModel> SearchAsync(string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        
        value = value.Trim().ToLowerInvariant();

        foreach (var connection in _connections.Connections)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            
            if (connection.Name.ToLowerInvariant().Contains(value))
            {
                yield return new SearchModel
                {
                    Name = connection.Name,
                    Description = connection.ConnectionString,
                    Icon = AionIcons.Connection,
                    Kind = ResultKind.Connection,
                    SearchAction = () => ConnectionAction(connection),
                };
            }

            foreach (var database in connection.Databases)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                
                if (database.Name.ToLowerInvariant().Contains(value))
                {
                    yield return new SearchModel
                    {
                        Name = $"{database.Name}",
                        Description = $"Connection: {connection.Name}",
                        Icon = AionIcons.Connection,
                        Kind = ResultKind.Database,
                        SearchAction = () => DatabaseAction(connection, database.Name)
                    };
                }

                if (database.TablesLoaded)
                {
                    foreach (var table in database.Tables)
                    {
                        if (cancellationToken.IsCancellationRequested) yield break;
                        
                        if (table.ToLowerInvariant().Contains(value))
                        {
                            yield return new SearchModel
                            {
                                Name = $"{table}",
                                Description = $"{connection.Name} > {database.Name}",
                                Icon = AionIcons.Table,
                                Kind = ResultKind.Table,
                                SearchAction = () => TableAction(connection, database, table)
                            };
                        }
                    }
                }
                else
                {
                   
                        await _connections.LoadTablesAsync(connection, database);
                        
                        foreach (var table in database.Tables)
                        {
                            if (cancellationToken.IsCancellationRequested) yield break;
                            
                            if (table.ToLowerInvariant().Contains(value))
                            {
                                yield return new SearchModel
                                {
                                    Name = $"{table}",
                                    Description = $"{connection.Name} > {database.Name}",
                                    Icon = AionIcons.Table,
                                    Kind = ResultKind.Table,
                                    SearchAction = () => TableAction(connection, database, table)
                                };
                            }
                        }
                }
            }
        }

        foreach (var query in _queries.Queries)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            
            if (query.Name.ToLowerInvariant().Contains(value) || 
                query.Query.ToLowerInvariant().Contains(value))
            {
                yield return new SearchModel
                {
                    Name = query.Name,
                    Description = $"Connection: {query.ConnectionId} > Database: {query.DatabaseName}",
                    Icon = AionIcons.Query,
                    Kind = ResultKind.Query,
                    SearchAction = () => QueryAction(query)
                };
            }
        }
    }

    private async Task ConnectionAction(ConnectionModel connection)
    {
        await _bus.PublishAsync(new ActivatePanel(connection.Id.ToString()));
    }
    
    private async Task DatabaseAction(ConnectionModel connection, string databaseName)
    {
        await _bus.PublishAsync(new ActivatePanel(connection.Id.ToString()));
        await Task.Delay(25);
        await _bus.PublishAsync(new ExpandDatabase(connection, databaseName));
    }
    
    private async Task TableAction(ConnectionModel connection, DatabaseModel database, string tableName)
    {
        await _bus.PublishAsync(new ActivatePanel(connection.Id.ToString()));
        await Task.Delay(25);
        await _bus.PublishAsync(new ExpandDatabase(connection, database.Name));
        await Task.Delay(25);
        await _bus.PublishAsync(new ExpandTable(connection, database, tableName));
    }

    private async Task QueryAction(QueryModel query)
    {
        await _bus.PublishAsync(new FocusQuery(query));
    }
}