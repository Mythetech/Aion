using Aion.Components.Infrastructure;
using Microsoft.JSInterop;

namespace Aion.Web.Services;

public class BrowserLinkOpenService : ILinkOpenService
{
    private readonly IJSRuntime _js;

    public BrowserLinkOpenService(IJSRuntime js)
    {
        _js = js;
    }

    public async void OpenUrl(string url)
    {
        await _js.InvokeVoidAsync("open", url, "_blank");
    }
}
