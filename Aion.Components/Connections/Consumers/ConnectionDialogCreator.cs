using Aion.Components.Connections.Commands;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.Connections.Consumers;

public class ConnectionDialogCreator : IConsumer<PromptCreateConnection>
{
    private readonly IConnectionPrompt _prompt;

    public ConnectionDialogCreator(IConnectionPrompt prompt)
    {
        _prompt = prompt;
    }

    public async Task Consume(PromptCreateConnection message)
    {
        await _prompt.PromptAsync(message.InitialValues);
    }
}
