using Aion.Components.Settings;
using Aion.Components.Settings.Domains;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.Environment;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings.Events;
using NSubstitute;

namespace Aion.Test.Components.Settings;

public class SettingsDialogTests : TestContext
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    public SettingsDialogTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddSingleton(_bus);
        Services.AddAionSettings();

        var runtime = Substitute.For<IRuntimeEnvironment>();
        runtime.Platform.Returns(Platform.Desktop);
        Services.AddSingleton(runtime);
    }

    private async Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync()
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync<SettingsDialog>("Settings"));
        return provider;
    }

    [Fact]
    public async Task ChoosingATheme_SavesOnlyAppearanceWithoutPressingDone()
    {
        var provider = await OpenDialogAsync();

        await provider.FindAll("button[role=checkbox]")
            .First(b => b.TextContent.Contains("Dark"))
            .ClickAsync(new MouseEventArgs());

        await _bus.Received(1).PublishAsync(Arg.Is<SettingsModelChanged>(m => m.Settings is AppearanceSettings));
        await _bus.DidNotReceive().PublishAsync(Arg.Is<SettingsModelChanged>(m => !(m.Settings is AppearanceSettings)));
    }
}
