using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Shared.Dialogs.Commands;

public record ShowHelp();

public class HelpShower : IConsumer<ShowHelp>
{
    private readonly IMessageBus _bus;

    public HelpShower(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Consume(ShowHelp message)
    {
        await _bus.PublishAsync(new ShowDialog(typeof(HelpDialog), "Support"));
    }
}