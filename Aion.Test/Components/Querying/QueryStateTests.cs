using Mythetech.Framework.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
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
        _state.Queries[0].Query.ShouldBeEmpty();
        _state.Queries[0].IsDirty.ShouldBeFalse();
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
    public void ChoosingAConnectionWithOneDatabase_SelectsIt()
    {
        // Arrange
        var query = _state.Queries[0];
        var connection = new ConnectionModel
        {
            Type = DatabaseType.WasmSQLite,
            ConnectionString = "Data Source=sample_store.db",
            Databases = [new DatabaseModel { Name = "sample_store" }]
        };

        // Act
        _state.UpdateQueryConnection(query, connection);

        // Assert
        query.DatabaseName.ShouldBe("sample_store");
    }

    [Fact]
    public void ChoosingAConnectionThatNamesADatabase_SelectsIt()
    {
        // Arrange
        var query = _state.Queries[0];
        var connection = new ConnectionModel
        {
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=localhost;Database=Sales;Username=app",
            Databases = [new DatabaseModel { Name = "postgres" }, new DatabaseModel { Name = "sales" }]
        };

        // Act
        _state.UpdateQueryConnection(query, connection);

        // Assert
        query.DatabaseName.ShouldBe("sales");
    }

    [Fact]
    public void ChoosingAServerWithSeveralDatabasesAndNoneNamed_LeavesTheChoiceToTheUser()
    {
        // Arrange
        var query = _state.Queries[0];
        query.DatabaseName = "old";
        var connection = new ConnectionModel
        {
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=localhost;Username=app",
            Databases = [new DatabaseModel { Name = "postgres" }, new DatabaseModel { Name = "sales" }]
        };

        // Act
        _state.UpdateQueryConnection(query, connection);

        // Assert
        query.DatabaseName.ShouldBeNull();
    }

    [Fact]
    public void ChoosingADatabaseOnAnotherConnection_MovesTheTabThereInOneChange()
    {
        // Arrange
        var query = _state.Queries[0];
        query.DatabaseName = "old";
        var connection = new ConnectionModel
        {
            Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=localhost;Database=postgres",
            Databases = [new DatabaseModel { Name = "postgres" }, new DatabaseModel { Name = "sales" }]
        };
        var changes = 0;
        _state.StateChanged += () => changes++;

        // Act
        _state.UpdateQueryConnection(query, connection, "sales");

        // Assert
        query.ConnectionId.ShouldBe(connection.Id);
        query.DatabaseName.ShouldBe("sales");
        changes.ShouldBe(1);
    }

    [Fact]
    public void RunToggles_ChangeTheTabAndTellTheEditor()
    {
        // Arrange
        var query = _state.Queries[0];
        var changes = 0;
        _state.StateChanged += () => changes++;

        // Act
        _state.SetUseTransaction(query, true);
        _state.SetIncludeEstimatedPlan(query, true);
        _state.SetIncludeActualPlan(query, true);

        // Assert
        query.UseTransaction.ShouldBeTrue();
        query.IncludeEstimatedPlan.ShouldBeTrue();
        query.IncludeActualPlan.ShouldBeTrue();
        changes.ShouldBe(3);
    }

    [Fact]
    public void TransactionsToggle_WhileATransactionIsOpen_StaysOn()
    {
        // Arrange
        var query = _state.Queries[0];
        query.UseTransaction = true;
        query.Transaction = new TransactionInfo();

        // Act
        _state.SetUseTransaction(query, false);

        // Assert
        query.UseTransaction.ShouldBeTrue();
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
    public void Should_Keep_Executing_State_When_Switching_Tabs()
    {
        // Arrange
        var running = _state.Queries[0];
        running.IsExecuting = true;
        var other = _state.AddQuery("Other");

        // Act
        _state.SetActive(other);
        _state.SetActive(running);

        // Assert
        _state.Active!.IsExecuting.ShouldBeTrue();
    }

    [Fact]
    public void Should_Not_Share_Open_Transaction_When_Cloning()
    {
        // Arrange
        var original = _state.Queries[0];
        original.Transaction = new TransactionInfo();

        // Act
        var clone = _state.Clone(original);

        // Assert
        clone.Transaction.ShouldBeNull();
        original.HasOpenTransaction.ShouldBeTrue();
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

    [Fact]
    public void Should_Reorder_Query()
    {
        // Arrange
        var q1 = _state.Queries[0];
        var q2 = _state.AddQuery("Query 2");
        var q3 = _state.AddQuery("Query 3");

        // Act - move q3 to position 0
        _state.ReorderQuery(q3.Id, 0);

        // Assert
        _state.Queries[0].ShouldBe(q3);
        _state.Queries[1].ShouldBe(q1);
        _state.Queries[2].ShouldBe(q2);
        _state.Queries[0].Order.ShouldBe(0);
        _state.Queries[1].Order.ShouldBe(1);
        _state.Queries[2].Order.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Close_Others()
    {
        // Arrange
        var q1 = _state.Queries[0];
        var q2 = _state.AddQuery("Query 2");
        var q3 = _state.AddQuery("Query 3");

        // Act
        await _state.CloseOthers(q2);

        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries[0].ShouldBe(q2);
        _state.Active.ShouldBe(q2);
        await _messageBus.Received().PublishAsync(Arg.Is<DeleteQuery>(cmd => cmd.Query == q1));
        await _messageBus.Received().PublishAsync(Arg.Is<DeleteQuery>(cmd => cmd.Query == q3));
    }

    [Fact]
    public async Task Should_Close_All_Tabs_And_Add_Default()
    {
        // Arrange
        var q1 = _state.Queries[0];
        _state.AddQuery("Query 2");

        // Act
        await _state.CloseAllTabs();

        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries[0].Id.ShouldNotBe(q1.Id);
        _state.Active.ShouldBe(_state.Queries[0]);
    }

    [Fact]
    public async Task Should_Close_To_Right()
    {
        // Arrange
        var q1 = _state.Queries[0];
        var q2 = _state.AddQuery("Query 2");
        var q3 = _state.AddQuery("Query 3");

        // Act
        await _state.CloseToRight(q1);

        // Assert
        _state.Queries.Count.ShouldBe(1);
        _state.Queries[0].ShouldBe(q1);
        await _messageBus.Received().PublishAsync(Arg.Is<DeleteQuery>(cmd => cmd.Query == q2));
        await _messageBus.Received().PublishAsync(Arg.Is<DeleteQuery>(cmd => cmd.Query == q3));
    }

    [Fact]
    public void Should_Track_Dirty_State()
    {
        // Arrange
        var query = _state.AddQuery("Test");

        // Assert - initially clean
        query.IsDirty.ShouldBeFalse();

        // Act - modify query text
        query.Query = "SELECT 1";

        // Assert - now dirty
        query.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_WritesTheTabAndClearsItsUnsavedMark()
    {
        // Arrange
        var query = _state.AddQuery("Test");
        _state.EditQueryText(query, "SELECT 1");
        query.IsDirty.ShouldBeTrue();

        // Act
        await _state.SaveAsync(query);

        // Assert
        await _saveService.Received(1).SaveQueryAsync(query);
        query.IsDirty.ShouldBeFalse();
        query.SavedQuery.ShouldBe("SELECT 1");
    }

    [Fact]
    public async Task SaveAsync_WhenStorageFails_KeepsTheUnsavedMark()
    {
        // Arrange
        var query = _state.AddQuery("Test");
        _state.EditQueryText(query, "SELECT 1");
        _saveService.SaveQueryAsync(query).Returns(Task.FromException(new IOException("disk full")));

        // Act
        await Should.ThrowAsync<IOException>(() => _state.SaveAsync(query));

        // Assert
        query.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task TextTypedWhileASaveIsInFlight_StaysUnsaved()
    {
        // Arrange
        var query = _state.AddQuery("Test");
        _state.EditQueryText(query, "SELECT 1");
        var write = new TaskCompletionSource();
        _saveService.SaveQueryAsync(query).Returns(write.Task);

        // Act
        var save = _state.SaveAsync(query);
        _state.EditQueryText(query, "SELECT 12");
        write.SetResult();
        await save;

        // Assert
        query.SavedQuery.ShouldBe("SELECT 1");
        query.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void EditQueryText_RaisesStateChangedOnlyWhenTheUnsavedMarkChanges()
    {
        // Arrange
        var query = _state.AddQuery("Test");
        var raised = 0;
        _state.StateChanged += () => raised++;

        // Act
        _state.EditQueryText(query, "S");
        _state.EditQueryText(query, "SE");
        _state.EditQueryText(query, "SEL");
        _state.EditQueryText(query, "");

        // Assert
        raised.ShouldBe(2);
        query.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void EditQueryText_DoesNotAskTheEditorToReload()
    {
        // Arrange
        var query = _state.Queries[0];
        _state.SetActive(query);
        var reloaded = false;
        _state.ActiveQueryTextChanged += () => { reloaded = true; return Task.CompletedTask; };

        // Act
        _state.EditQueryText(query, "SELECT 1");

        // Assert
        query.Query.ShouldBe("SELECT 1");
        reloaded.ShouldBeFalse();
    }

    private static async Task StopAsync(CancellationTokenSource stop, Task autoSave)
    {
        await stop.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => autoSave);
    }

    [Fact]
    public async Task Typing_IsSavedOnceEditsPauseAndTheMarkClears()
    {
        // Arrange
        var state = new QueryState(_messageBus, _saveService, autoSaveDelay: TimeSpan.FromMilliseconds(20));
        await state.InitializeAsync();
        var query = state.Queries[0];
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.StateChanged += () =>
        {
            if (!query.IsDirty && query.Query == "SELECT 1") saved.TrySetResult();
        };
        using var stop = new CancellationTokenSource();
        var autoSave = state.SaveWhenEditsPauseAsync(stop.Token);

        // Act
        state.EditQueryText(query, "SELECT");
        state.EditQueryText(query, "SELECT 1");
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        query.SavedQuery.ShouldBe("SELECT 1");
        await _saveService.Received().SaveQueryAsync(query);
        await StopAsync(stop, autoSave);
    }

    [Fact]
    public async Task AutoSave_DoesNotRunBeforeTheSavedTabsAreLoaded()
    {
        // Arrange
        var state = new QueryState(_messageBus, _saveService, autoSaveDelay: TimeSpan.Zero);
        using var stop = new CancellationTokenSource();
        var autoSave = state.SaveWhenEditsPauseAsync(stop.Token);

        // Act
        state.EditQueryText(state.Queries[0], "SELECT 1");
        await Task.Delay(50);

        // Assert
        await _saveService.DidNotReceive().SaveQueryAsync(Arg.Any<QueryModel>());
        await StopAsync(stop, autoSave);
    }

    [Fact]
    public async Task AutoSave_SkipsATabClosedBeforeItsTurn()
    {
        // Arrange
        var state = new QueryState(_messageBus, _saveService, autoSaveDelay: TimeSpan.Zero);
        var first = state.Queries[0];
        var second = state.AddQuery("Second");
        var firstWrite = new TaskCompletionSource();
        var firstWriteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _saveService.SaveQueryAsync(first).Returns(_ =>
        {
            firstWriteStarted.TrySetResult();
            return firstWrite.Task;
        });
        using var stop = new CancellationTokenSource();
        var autoSave = state.SaveWhenEditsPauseAsync(stop.Token);

        // Act
        await state.InitializeAsync();
        await firstWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await state.Remove(second);
        firstWrite.SetResult();
        await Task.Delay(50);

        // Assert
        await _saveService.DidNotReceive().SaveQueryAsync(second);
        await StopAsync(stop, autoSave);
    }

    [Fact]
    public async Task AutoSave_NeverWritesBackATabWhoseDeleteIsInFlight()
    {
        // Arrange
        var state = new QueryState(_messageBus, _saveService, autoSaveDelay: TimeSpan.Zero);
        await state.InitializeAsync();
        var first = state.Queries[0];
        var second = state.AddQuery("Second");
        var delete = new TaskCompletionSource();
        _messageBus.PublishAsync(Arg.Any<DeleteQuery>()).Returns(delete.Task);
        var firstSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _saveService.SaveQueryAsync(first).Returns(_ =>
        {
            firstSaved.TrySetResult();
            return Task.CompletedTask;
        });
        using var stop = new CancellationTokenSource();
        var autoSave = state.SaveWhenEditsPauseAsync(stop.Token);
        await firstSaved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _saveService.ClearReceivedCalls();

        // Act
        var closing = state.Remove(second);
        state.EditQueryText(first, "SELECT 1");
        await Task.Delay(100);
        delete.SetResult();
        await closing;

        // Assert
        await _saveService.Received().SaveQueryAsync(first);
        await _saveService.DidNotReceive().SaveQueryAsync(second);
        await StopAsync(stop, autoSave);
    }

    [Fact]
    public async Task AutoSave_ThatClearsNoUnsavedMark_RaisesNoStateChanged()
    {
        // Arrange
        var state = new QueryState(_messageBus, _saveService, autoSaveDelay: TimeSpan.Zero);
        await state.InitializeAsync();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _saveService.SaveQueryAsync(Arg.Any<QueryModel>()).Returns(_ =>
        {
            saved.TrySetResult();
            return Task.CompletedTask;
        });
        var raised = 0;
        state.StateChanged += () => Interlocked.Increment(ref raised);
        using var stop = new CancellationTokenSource();
        var autoSave = state.SaveWhenEditsPauseAsync(stop.Token);

        // Act
        state.RenameQuery(state.Queries[0], "Renamed");
        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        // Assert: only the rename itself was announced, so views such as the results grid aren't reset.
        raised.ShouldBe(1);
        await StopAsync(stop, autoSave);
    }

    [Fact]
    public void HasSql_IsFalseForATabWithOnlyWhitespace()
    {
        // Arrange
        var blank = new QueryModel { Query = "  \n\t" };
        var written = new QueryModel { Query = "SELECT 1" };

        // Assert
        blank.HasSql.ShouldBeFalse();
        written.HasSql.ShouldBeTrue();
    }

    [Fact]
    public void Should_Assign_Order_On_Add()
    {
        // Arrange - default query already at index 0
        var q1 = _state.Queries[0];

        // Act
        var q2 = _state.AddQuery("Query 2");
        var q3 = _state.AddQuery("Query 3");

        // Assert
        q1.Order.ShouldBe(0);
        q2.Order.ShouldBe(1);
        q3.Order.ShouldBe(2);
    }

    [Fact]
    public async Task InitializeAsync_CalledAgainWhileLoading_WaitsForTheSavedTabs()
    {
        // Arrange
        var load = new TaskCompletionSource<IEnumerable<QueryModel>>();
        _saveService.LoadQueriesAsync().Returns(load.Task);
        var saved = new QueryModel { Name = "Saved", Query = "SELECT 1" };

        // Act
        var first = _state.InitializeAsync();
        var second = _state.InitializeAsync();
        var secondFinishedEarly = second.IsCompleted;
        load.SetResult([saved]);
        await Task.WhenAll(first, second);

        // Assert
        secondFinishedEarly.ShouldBeFalse();
        _state.Active.ShouldBe(saved);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSavedTabsCannotBeRead_KeepsTheDefaultTab()
    {
        // Arrange
        _saveService.LoadQueriesAsync().Returns(Task.FromException<IEnumerable<QueryModel>>(new IOException("unreadable")));

        // Act
        await _state.InitializeAsync();

        // Assert
        _state.Active.ShouldBe(_state.Queries.Single());
    }

    [Fact]
    public async Task Should_Normalize_Order_After_Initialize()
    {
        // Arrange
        var savedQueries = new List<QueryModel>
        {
            new() { Name = "A", Order = 5 },
            new() { Name = "B", Order = 10 },
            new() { Name = "C", Order = 15 }
        };
        _saveService.LoadQueriesAsync().Returns(savedQueries);

        // Act
        await _state.InitializeAsync();

        // Assert
        _state.Queries[0].Order.ShouldBe(0);
        _state.Queries[1].Order.ShouldBe(1);
        _state.Queries[2].Order.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Set_SavedQuery_On_Initialize()
    {
        // Arrange
        var savedQueries = new List<QueryModel>
        {
            new() { Name = "A", Query = "SELECT 1" }
        };
        _saveService.LoadQueriesAsync().Returns(savedQueries);

        // Act
        await _state.InitializeAsync();

        // Assert
        _state.Queries[0].SavedQuery.ShouldBe("SELECT 1");
        _state.Queries[0].IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SetResult_WithASelectionStart_MovesTheErrorIntoTheWholeText()
    {
        // Arrange
        var query = _state.Queries[0];
        var result = new QueryResult();
        result.SetError(QueryErrorNormalizer.Normalize("no such column: x", "SELECT x FROM t"));

        // Act
        _state.SetResult(query, result, executedFrom: (4, 3));

        // Assert
        query.Result!.ErrorDetail!.Line.ShouldBe(4);
        query.Result.ErrorDetail.Column.ShouldBe(10);
    }

    [Fact]
    public void ErrorLocation_IsCurrentUntilTheTabsSqlChanges()
    {
        // Arrange
        var query = _state.Queries[0];
        query.Query = "SELECT x FROM t";
        var result = new QueryResult();
        result.SetError(QueryErrorNormalizer.Normalize("no such column: x", query.Query));

        // Act
        _state.SetResult(query, result);
        var beforeEdit = query.ErrorLocationIsCurrent;
        query.Query = "SELECT y FROM t";

        // Assert
        beforeEdit.ShouldBeTrue();
        query.ErrorLocationIsCurrent.ShouldBeFalse();
    }

    [Fact]
    public void ErrorLocation_WithoutAPosition_IsNeverCurrent()
    {
        // Arrange
        var query = _state.Queries[0];
        var result = new QueryResult();
        result.SetError(QueryErrorNormalizer.Normalize("division by zero", query.Query));

        // Act
        _state.SetResult(query, result);

        // Assert
        query.ErrorLocationIsCurrent.ShouldBeFalse();
    }
}
