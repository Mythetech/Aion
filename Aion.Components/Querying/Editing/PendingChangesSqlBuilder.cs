using Aion.Components.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries.Editing;

namespace Aion.Components.Querying.Editing;

/// <summary>
/// The statements for a set of pending changes, together with the provider and connection string that will run them.
/// </summary>
public record PendingChangesSql(IDatabaseProvider Provider, string ConnectionString, SqlGenerationResult Generation);

/// <summary>
/// Resolves where pending edits will run and generates their SQL. The preview and the applier both go through
/// here so the SQL a user reviews is exactly the SQL that executes.
/// </summary>
public class PendingChangesSqlBuilder
{
    private readonly ConnectionState _connectionState;
    private readonly ISqlChangeGenerator _generator;

    public PendingChangesSqlBuilder(ConnectionState connectionState, ISqlChangeGenerator generator)
    {
        _connectionState = connectionState;
        _generator = generator;
    }

    public async Task<PendingChangesSql> BuildAsync(EditableQueryResult result, IEnumerable<PendingChange> changes)
    {
        var connection = _connectionState.Connections.FirstOrDefault(c => c.Id == result.ConnectionId)
            ?? throw new InvalidOperationException("Connection not found");

        var provider = _connectionState.GetProvider(connection.Type);
        if (provider is not IDatabaseRowEditingProvider)
        {
            throw new InvalidOperationException($"Editing rows is not supported for {connection.Type} connections");
        }

        var connectionString = provider.UpdateConnectionString(connection.ConnectionString, result.SourceDatabase ?? "");
        var generation = await _generator.GenerateSqlAsync(result, changes, provider.Commands);

        return new PendingChangesSql(provider, connectionString, generation);
    }
}
