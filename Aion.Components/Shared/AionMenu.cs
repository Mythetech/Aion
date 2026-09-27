using MudBlazor;

namespace Aion.Components.Shared;

/// <summary>
/// A <see cref="MudMenu"/> with Aion's defaults: dense items, <c>pa-1</c> on the popover list so the items are
/// inset from its edge, and a popover that flips above its anchor when there is no room below, instead of running
/// off the bottom of the window. Every <see cref="MudMenu"/> parameter still applies, passing
/// <see cref="MudMenu.Dense"/> or <see cref="MudMenu.ListClass"/> overrides the default, and a
/// <see cref="MudMenu.PopoverClass"/> is added to rather than replacing the flip.
/// </summary>
public class AionMenu : MudMenu
{
    public const string FlipWhenOutOfRoomClass = PopoverFlip.WhenOutOfRoomClass;

    public AionMenu()
    {
        Dense = true;
        ListClass = "pa-1";
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        PopoverClass = PopoverFlip.AddTo(PopoverClass);
    }
}
