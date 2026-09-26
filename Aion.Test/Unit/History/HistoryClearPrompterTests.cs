using Aion.Components.History;
using Aion.Components.History.Consumers;
using Aion.Components.NativeMenu;
using Aion.Components.Shared.Dialogs.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.History;

public class HistoryClearPrompterTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly HistoryState _history = new(new InMemoryQueryHistoryStore(), NullLogger<HistoryState>.Instance);
    private readonly HistoryClearPrompter _prompter;

    public HistoryClearPrompterTests()
    {
        _prompter = new HistoryClearPrompter(_history, _bus);
    }

    [Fact]
    public async Task WithEntries_AsksForConfirmationWithoutClearing()
    {
        await _history.AddAsync(HistoryEntries.Success("SELECT 1"));

        await _prompter.Consume(new ClearHistory());

        await _bus.Received(1).PublishAsync(Arg.Is<ShowDialog>(d => d.Dialog == typeof(ClearHistoryDialog)));
        _history.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WhenEmpty_NotifiesInsteadOfAsking()
    {
        await _prompter.Consume(new ClearHistory());

        await _bus.DidNotReceive().PublishAsync(Arg.Any<ShowDialog>());
        await _bus.Received(1).PublishAsync(Arg.Any<AddNotification>());
    }
}
