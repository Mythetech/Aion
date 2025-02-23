using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Components.Theme;
using Microsoft.Extensions.Logging;

namespace Aion.Components.Search;

public class SearchService
{
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;
    private readonly ILogger<SearchService> _logger;

    public SearchService(ConnectionState connections, QueryState queries, ILogger<SearchService> logger)
    {
        _connections = connections;
        _queries = queries;
        _logger = logger;
    }

    public async IAsyncEnumerable<SearchModel> SearchAsync(string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;
        
        value = value.Trim().ToLowerInvariant();

        // Search connections
        foreach (var connection in _connections.Connections)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            
            if (connection.Name.ToLowerInvariant().Contains(value))
            {
                yield return new SearchModel
                {
                    Name = connection.Name,
                    Icon = AionIcons.Connection,
                    Kind = ResultKind.Connection
                };
            }

            // Search databases
            foreach (var database in connection.Databases)
            {
                if (cancellationToken.IsCancellationRequested) yield break;
                
                if (database.Name.ToLowerInvariant().Contains(value))
                {
                    yield return new SearchModel
                    {
                        Name = $"{connection.Name} > {database.Name}",
                        Icon = AionIcons.Connection,
                        Kind = ResultKind.Database
                    };
                }

                // Search loaded tables
                if (database.TablesLoaded)
                {
                    foreach (var table in database.Tables)
                    {
                        if (cancellationToken.IsCancellationRequested) yield break;
                        
                        if (table.ToLowerInvariant().Contains(value))
                        {
                            yield return new SearchModel
                            {
                                Name = $"{connection.Name} > {database.Name} > {table}",
                                Icon = AionIcons.Table,
                                Kind = ResultKind.Table
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
                                    Name = $"{connection.Name} > {database.Name} > {table}",
                                    Icon = AionIcons.Table,
                                    Kind = ResultKind.Table
                                };
                            }
                        }
                }
            }
        }

        // Search queries
        foreach (var query in _queries.Queries)
        {
            if (cancellationToken.IsCancellationRequested) yield break;
            
            if (query.Name.ToLowerInvariant().Contains(value) || 
                query.Query.ToLowerInvariant().Contains(value))
            {
                yield return new SearchModel
                {
                    Name = query.Name,
                    Icon = AionIcons.Query,
                    Kind = ResultKind.Query
                };
            }
        }
    }
}