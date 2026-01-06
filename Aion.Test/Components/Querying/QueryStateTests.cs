using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Core.Connections;
using Aion.Core.Queries;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.Querying;

public class QueryStateTests
{
    private readonly IMessageBus _messageBus;
    private readonly IQuerySaveService _saveService;
    private readonly QueryState _state;

    public QueryStateTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _saveService = Substitute.For<IQuerySaveService>();
        _state = new QueryState(_messageBus, _saveService);
    }

    [Fact]
    public void Should_Initialize_With_Default_Query()
    {
        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries[0].Name.ShouldBe("Query1");
    }

    [Fact]
    public async Task Should_Load_Saved_Queries_On_Initialize()
    {
        // Arrange
        var savedQueries = new List<QueryModel>
        {
            new() { Name = "Saved Query 1" },
            new() { Name = "Saved Query 2" }
        };
        _saveService.LoadQueriesAsync().Returns(savedQueries);

        // Act
        await _state.InitializeAsync();

        // Assert
        _state.Queries.Count.ShouldBe(2);
        _state.Queries[0].Name.ShouldBe("Saved Query 1");
        _state.Active.ShouldBe(_state.Queries[0]);
    }

    [Fact]
    public void Should_Add_New_Query()
    {
        // Act
        var query = _state.AddQuery("Test Query");

        // Assert
        _state.Queries.Count.ShouldBe(2);
        query.Name.ShouldBe("Test Query");
        _state.Active.ShouldBe(query);
    }

    [Fact]
    public void Should_Clone_Query()
    {
        // Arrange
        var original = _state.Queries[0];
        original.Query = "SELECT * FROM Users";
        original.ConnectionId = Guid.NewGuid();
        original.DatabaseName = "TestDB";

        // Act
        var clone = _state.Clone(original);

        // Assert
        clone.ShouldNotBe(original);
        clone.Id.ShouldNotBe(original.Id);
        clone.Query.ShouldBe(original.Query);
        clone.ConnectionId.ShouldBe(original.ConnectionId);
        clone.DatabaseName.ShouldBe(original.DatabaseName);
        _state.Active.ShouldBe(clone);
    }

    [Fact]
    public async Task Should_Remove_Query_And_Set_New_Active()
    {
        // Arrange
        var query1 = _state.Queries[0];
        var query2 = _state.AddQuery("Query 2");
        _state.SetActive(query1);

        // Act
        await _state.Remove(query1);

        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries.ShouldNotContain(query1);
        _state.Active.ShouldBe(query2);
        await _messageBus.Received().PublishAsync(Arg.Is<DeleteQuery>(cmd => cmd.Query == query1));
    }

    [Fact]
    public async Task Should_Add_New_Query_When_Removing_Last_One()
    {
        // Arrange
        var query = _state.Queries[0];

        // Act
        await _state.Remove(query);

        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries[0].ShouldNotBe(query);
        _state.Active.ShouldBe(_state.Queries[0]);
    }

    [Fact]
    public void Should_Update_Query_Connection()
    {
        // Arrange
        var query = _state.Queries[0];
        var connection = new ConnectionModel { Id = Guid.NewGuid() };
        query.DatabaseName = "OldDB";

        // Act
        _state.UpdateQueryConnection(query, connection);

        // Assert
        query.ConnectionId.ShouldBe(connection.Id);
        query.DatabaseName.ShouldBeNull();
    }

    [Fact]
    public void Should_Update_Query_Database()
    {
        // Arrange
        var query = _state.Queries[0];

        // Act
        _state.UpdateQueryDatabase(query, "NewDB");

        // Assert
        query.DatabaseName.ShouldBe("NewDB");
    }

    [Fact]
    public async Task Should_Update_Query_Text_And_Notify_If_Active()
    {
        // Arrange
        var query = _state.Queries[0];
        _state.SetActive(query);
        var notified = false;
        _state.ActiveQueryTextChanged += () => { notified = true; return Task.CompletedTask; };

        // Act
        await _state.UpdateQueryText(query, "SELECT 1");

        // Assert
        query.Query.ShouldBe("SELECT 1");
        notified.ShouldBeTrue();
    }

    [Fact]
    public void Should_Rename_Query()
    {
        // Arrange
        var query = _state.Queries[0];

        // Act
        _state.RenameQuery(query, "New Name");

        // Assert
        query.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Should_Set_Transaction_Info()
    {
        // Arrange
        var query = _state.Queries[0];
        _state.SetActive(query);
        var transaction = new TransactionInfo {Id = Guid.NewGuid().ToString(), StartTime = DateTime.Now, Status = TransactionStatus.Active};

        // Act
        _state.SetTransactionInfo(transaction);

        // Assert
        _state.Active.Transaction.ShouldBe(transaction);
    }

    [Fact]
    public void Should_Set_Query_Result()
    {
        // Arrange
        var query = _state.Queries[0];
        var result = new QueryResult();

        // Act
        _state.SetResult(query, result);

        // Assert
        query.Result.ShouldBe(result);
    }
}