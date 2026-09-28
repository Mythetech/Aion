using Microsoft.JSInterop;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.Smoke;

namespace Aion.Desktop.Smoke;

/// <summary>
/// Passes when Monaco and BlazorMonaco's interop script both loaded in the WebView. A publish that drops a static
/// asset still renders the shell, and the query editor's guard just keeps waiting, so without this the smoke run
/// would pass an app whose editor never opens.
/// </summary>
public sealed class EditorScriptsSmokeCheck(IJSRuntime js, IJsGuardService jsGuards) : ISmokeCheck
{
    private const string MonacoGuard = "monaco";

    // Shorter than the check's own timeout so a missing Monaco fails with its own message, not a timeout.
    private static readonly TimeSpan GuardTimeout = TimeSpan.FromSeconds(8);

    public string Name => "aion/editor-scripts";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!await jsGuards.WaitForReadyAsync(js, MonacoGuard, GuardTimeout))
            throw new InvalidOperationException("Monaco did not load in the WebView");

        bool loaded;
        try
        {
            loaded = await js.InvokeAsync<bool>("aionEditorScripts.loaded", cancellationToken);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException("The Aion index page script that reports editor scripts did not load", ex);
        }

        if (!loaded)
            throw new InvalidOperationException("Monaco loaded but BlazorMonaco's interop script did not");
    }
}
