using Aion.Components.History;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Aion.Test.Unit.History;

public class HistoryStateTests
{
    private readonly InMemoryQueryHistoryStore _store = new();
    private readonly HistoryState _state;

    public HistoryStateTests()
    {
        _state = new HistoryState(_store, NullLogger<HistoryState>.Instance);
    }

    [Fact]
    public async Task AddAsync_KeepsNewestFirstAndPersists()
    {
        var first = HistoryEntries.Success("SELECT 1", HistoryEntries.Now.AddMinutes(-2));
        var second = HistoryEntries.Success("SELECT 2", HistoryEntries.Now);

        await _state.AddAsync(first);
        await _state.AddAsync(second);

        _state.Entries.ShouldBe([second, first]);
        _store.Saved.ShouldBe([second, first]);
    }

    [Fact]
    public async Task AddAsync_DropsOldestEntriesBeyondTheCap()
    {
        var entries = Enumerable.Range(0, HistoryState.MaxEntries + 5)
            .Select(i => HistoryEntries.Success($"SELECT {i}", HistoryEntries.Now.AddSeconds(i)))
            .ToList();

        foreach (var entry in entries)
        {
            await _state.AddAsync(entry);
        }

        _state.Entries.Count.ShouldBe(HistoryState.MaxEntries);
        _state.Entries[0].Sql.ShouldBe($"SELECT {HistoryState.MaxEntries + 4}");
        _state.Entries.ShouldNotContain(e => e.Sql == "SELECT 4");
        _state.Entries.ShouldContain(e => e.Sql == "SELECT 5");
        _store.Saved.Count.ShouldBe(HistoryState.MaxEntries);
    }

    [Fact]
    public async Task InitializeAsync_LoadsPersistedEntriesNewestFirstWithinTheCap()
    {
        _store.Saved = Enumerable.Range(0, HistoryState.MaxEntries + 10)
            .Select(i => HistoryEntries.Success($"SELECT {i}", HistoryEntries.Now.AddSeconds(-i)))
            .Reverse()
            .ToList();

        await _state.InitializeAsync();

        _state.Entries.Count.ShouldBe(HistoryState.MaxEntries);
        _state.Entries[0].Sql.ShouldBe("SELECT 0");
        _state.Entries[^1].Sql.ShouldBe($"SELECT {HistoryState.MaxEntries - 1}");
    }

    [Fact]
    public async Task AddAsync_BeforeInitialize_KeepsPersistedHistory()
    {
        var persisted = HistoryEntries.Success("SELECT persisted", HistoryEntries.Now.AddDays(-1));
        _store.Saved = [persisted];
        var added = HistoryEntries.Success("SELECT added");

        await _state.AddAsync(added);

        _state.Entries.ShouldBe([added, persisted]);
        _store.Saved.ShouldBe([added, persisted]);
    }

    [Fact]
    public async Task AddAsync_WhenLoadFails_DoesNotOverwritePersistedHistory_AndMergesOnceLoadSucceeds()
    {
        var persisted = HistoryEntries.Success("SELECT persisted", HistoryEntries.Now.AddDays(-1));
        _store.Saved = [persisted];
        _store.LoadException = new IOException("locked");
        var first = HistoryEntries.Success("SELECT first", HistoryEntries.Now.AddMinutes(-1));

        await _state.AddAsync(first);

        _state.Entries.ShouldBe([first]);
        _store.SaveCount.ShouldBe(0);

        _store.LoadException = null;
        var second = HistoryEntries.Success("SELECT second");
        await _state.AddAsync(second);

        _state.Entries.ShouldBe([second, first, persisted]);
        _store.Saved.ShouldBe([second, first, persisted]);
    }

    [Fact]
    public async Task AddAsync_WhenSaveFails_StillRecordsEntryAndRaisesHistoryChanged()
    {
        _store.SaveException = new IOException("disk full");
        var raised = 0;
        _state.HistoryChanged += () => raised++;
        var entry = HistoryEntries.Success("SELECT 1");

        await _state.AddAsync(entry);

        _state.Entries.ShouldBe([entry]);
        raised.ShouldBe(1);
    }

    [Fact]
    public async Task ClearAsync_RemovesEntriesAndPersistsTheEmptyList()
    {
        _store.Saved = [HistoryEntries.Success("SELECT persisted")];
        await _state.AddAsync(HistoryEntries.Success("SELECT 1"));
        var raised = 0;
        _state.HistoryChanged += () => raised++;

        await _state.ClearAsync();

        _state.Entries.ShouldBeEmpty();
        _store.Saved.ShouldBeEmpty();
        raised.ShouldBe(1);
    }

    [Fact]
    public async Task ClearAsync_BeforeInitialize_DiscardsPersistedHistory()
    {
        _store.Saved = [HistoryEntries.Success("SELECT persisted")];

        await _state.ClearAsync();
        await _state.InitializeAsync();

        _state.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_MatchesSqlCaseInsensitively()
    {
        await _state.AddAsync(HistoryEntries.Success("SELECT * FROM customers", HistoryEntries.Now.AddMinutes(-1)));
        await _state.AddAsync(HistoryEntries.Success("select * from products"));

        _state.Search("  PRODUCTS ").Select(e => e.Sql).ShouldBe(["select * from products"]);
        _state.Search("").Count.ShouldBe(2);
        _state.Search(null).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Search_ForAConnection_ReturnsOnlyItsEntries()
    {
        var reporting = Guid.NewGuid();
        await _state.AddAsync(HistoryEntries.Success("SELECT * FROM sales", HistoryEntries.Now.AddMinutes(-2)) with { ConnectionId = reporting });
        await _state.AddAsync(HistoryEntries.Success("SELECT * FROM users", HistoryEntries.Now.AddMinutes(-1)) with { ConnectionId = Guid.NewGuid() });
        await _state.AddAsync(HistoryEntries.Success("SELECT * FROM sales_archive") with { ConnectionId = reporting });

        _state.Search(null, reporting).Select(e => e.Sql).ShouldBe(["SELECT * FROM sales_archive", "SELECT * FROM sales"]);
        _state.Search("archive", reporting).Select(e => e.Sql).ShouldBe(["SELECT * FROM sales_archive"]);
    }
}
