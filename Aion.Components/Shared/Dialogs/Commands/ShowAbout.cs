using Aion.Components.AppContextPanel;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Shared.Dialogs.Commands;

public record ShowAbout();

public class AboutShower : IConsumer<ShowAbout>
{
    private readonly IMessageBus _bus;

    public AboutShower(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(ShowAbout message)
    {
        await _bus.PublishAsync(new ShowDialog(typeof(AboutAion), "About", AionDialogs.CreateDefaultOptions()));
    }
}
