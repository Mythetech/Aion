using Aion.Components.Settings.Commands;
using Aion.Components.Shared.Dialogs.Commands;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Settings.Consumers;

/// <summary>
/// Consumer that opens the settings dialog when the OpenSettingsDialog command is received.
/// </summary>
public class SettingsDialogOpener : IConsumer<OpenSettingsDialog>
{
    private readonly IMessageBus _bus;

    public SettingsDialogOpener(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(OpenSettingsDialog message)
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            BackgroundClass = "aion-dialog",
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseButton = false // We have custom close button in dialog
        };

        await _bus.PublishAsync(new ShowDialog(typeof(SettingsDialog), "Settings", options));
    }
}
