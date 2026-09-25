using Aion.Components.Connections;
using Aion.Web.Databases.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Web.Databases;

public class BrowserConnectionPrompt : IConnectionPrompt
{
    private readonly IMessageBus _bus;

    public BrowserConnectionPrompt(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task PromptAsync(ConnectionDialogModel? initialValues)
    {
        if (initialValues?.EditingConnectionId is not null)
        {
            await _bus.PublishAsync(ConnectionDialogPrompt.CreateDialog(initialValues));
            return;
        }

        // The browser can't reach database servers, so a new connection here is a new in-browser database.
        await _bus.PublishAsync(new CreateBrowserDatabase());
    }
}
