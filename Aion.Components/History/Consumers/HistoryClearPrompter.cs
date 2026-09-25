using Aion.Components.NativeMenu;
using Aion.Components.Shared.Dialogs;
using Aion.Components.Shared.Dialogs.Commands;
using Aion.Components.Shared.Snackbar.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.History.Consumers;

/// <summary>
/// Handles <see cref="ClearHistory"/> from the history panel, the command palette and the desktop menu,
/// so every host gets the same confirmation.
/// </summary>
public class HistoryClearPrompter : IConsumer<ClearHistory>
{
    private readonly HistoryState _state;
    private readonly IMessageBus _bus;

    public HistoryClearPrompter(HistoryState state, IMessageBus bus)
    {
        _state = state;
        _bus = bus;
    }

    public async Task Consume(ClearHistory message)
    {
        var count = _state.Entries.Count;
        if (count == 0)
        {
            await _bus.PublishAsync(new AddNotification("Query history is already empty", Severity.Info));
            return;
        }

        var parameters = new DialogParameters
        {
            { nameof(ClearHistoryDialog.EntryCount), count }
        };

        await _bus.PublishAsync(new ShowDialog(typeof(ClearHistoryDialog), "Clear History", AionDialogs.CreateDefaultOptions(), parameters));
    }
}
