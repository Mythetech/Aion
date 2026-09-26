using Aion.Components.CommandPalette;
using Aion.Components.CommandPalette.Commands;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Components.CommandPalette;
using Mythetech.Framework.Infrastructure.MessageBus;
using Shouldly;

namespace Aion.Test.Components.CommandPalette;

public class AionCommandPaletteTests : TestContext
{
    private readonly IMessageBus _bus;

    public AionCommandPaletteTests()
    {
        Services.AddLogging();
        Services.AddMudServices();
        Services.AddCommandPalette();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _bus = new InMemoryMessageBus(Services, new NullLogger<InMemoryMessageBus>(),
            Enumerable.Empty<IMessagePipe>(), Enumerable.Empty<IConsumerFilter>());
        Services.AddSingleton(_bus);
    }

    [Fact]
    public async Task OpenCommandPalette_ShowsThePalette()
    {
        // Arrange
        RenderComponent<MudPopoverProvider>();
        var dialogs = RenderComponent<MudDialogProvider>();
        RenderComponent<AionCommandPalette>();

        // Act
        await _bus.PublishAsync(new OpenCommandPalette());

        // Assert
        dialogs.WaitForAssertion(() => dialogs.FindComponents<CommandPalettePanel>().ShouldHaveSingleItem());
    }
}
