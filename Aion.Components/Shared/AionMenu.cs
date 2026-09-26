using MudBlazor;

namespace Aion.Components.Shared;

/// <summary>
/// A <see cref="MudMenu"/> with Aion's defaults: dense items, and <c>pa-1</c> on the popover list so the
/// items are inset from its edge. Every <see cref="MudMenu"/> parameter still applies, and passing
/// <see cref="MudMenu.Dense"/> or <see cref="MudMenu.ListClass"/> overrides the default.
/// </summary>
public class AionMenu : MudMenu
{
    public AionMenu()
    {
        Dense = true;
        ListClass = "pa-1";
    }
}
