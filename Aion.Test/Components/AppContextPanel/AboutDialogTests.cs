using Aion.Components.AppContextPanel;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Components.Links;
using Mythetech.Framework.Infrastructure;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Components.AppContextPanel;

public class AboutDialogTests : TestContext
{
    public AboutDialogTests()
    {
        Services.AddMudServices(x => x.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddSingleton(Substitute.For<ILinkOpenService>());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ShowAsync<TDialog>() where TDialog : Microsoft.AspNetCore.Components.ComponentBase
    {
        var provider = RenderComponent<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync<TDialog>("About"));
        return provider;
    }

    [Fact]
    public async Task AboutAion_OpensMythetechThroughExternalLink()
    {
        var provider = await ShowAsync<AboutAion>();

        provider.FindComponent<ExternalLink>().Instance.Link.ShouldBe("https://www.mythetech.com");
    }

    [Fact]
    public async Task AboutAionWasm_OpensMythetechThroughExternalLink()
    {
        var provider = await ShowAsync<AboutAionWasm>();

        provider.FindComponent<ExternalLink>().Instance.Link.ShouldBe("https://www.mythetech.com");
    }

    [Fact]
    public async Task AboutAion_HasNoEmptySubtitles()
    {
        var provider = await ShowAsync<AboutAion>();

        var subtitles = provider.FindAll(".mud-dialog-content h5, .mud-dialog-content h6");

        subtitles.Count.ShouldBe(2);
        subtitles.ShouldAllBe(heading => !string.IsNullOrWhiteSpace(heading.TextContent));
    }
}
