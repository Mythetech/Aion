using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Components.Querying.Consumers;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class QueryPersisterTests
{
    private readonly IQuerySaveService _saveService = Substitute.For<IQuerySaveService>();
    private readonly QueryState _state;
    private readonly QueryPersister _persister;

    public QueryPersisterTests()
    {
        _state = new QueryState(Substitute.For<IMessageBus>(), _saveService);
        _persister = new QueryPersister(_state, NullLogger<QueryPersister>.Instance);
    }

    [Fact]
    public async Task SaveQuery_ClearsTheActiveTabsUnsavedMark()
    {
        // Arrange
        var query = _state.Queries[0];
        _state.SetActive(query);
        _state.EditQueryText(query, "SELECT 1");

        // Act
        await _persister.Consume(new SaveQuery());

        // Assert
        await _saveService.Received(1).SaveQueryAsync(query);
        query.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAllQueries_SavesEveryTabAndClearsEveryMark()
    {
        // Arrange
        var first = _state.Queries[0];
        var second = _state.AddQuery("Second");
        _state.EditQueryText(first, "SELECT 1");
        _state.EditQueryText(second, "SELECT 2");

        // Act
        await _persister.Consume(new SaveAllQueries());

        // Assert
        await _saveService.Received(1).SaveQueryAsync(first);
        await _saveService.Received(1).SaveQueryAsync(second);
        first.IsDirty.ShouldBeFalse();
        second.IsDirty.ShouldBeFalse();
    }
}
