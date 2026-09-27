using MudBlazor;

namespace Aion.Components.Shared;

/// <summary>
/// A <see cref="MudSelect{T}"/> whose list flips above the field when there is no room for it below, instead of
/// running off the bottom of the window. Every <see cref="MudSelect{T}"/> parameter still applies, and a
/// <see cref="MudSelect{T}.PopoverClass"/> is added to rather than replacing the flip.
/// </summary>
public class AionSelect<T> : MudSelect<T>
{
    /// <summary>
    /// The class that restores popover flipping, which Aion turns off app-wide (see <see cref="PopoverFlip"/>).
    /// </summary>
    public const string FlipWhenOutOfRoomClass = PopoverFlip.WhenOutOfRoomClass;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        PopoverClass = PopoverFlip.AddTo(PopoverClass);
    }
}
