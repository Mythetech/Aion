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
    /// Aion turns popover flipping off app-wide (see <c>RegistrationExtensions</c>), and MudSelect has no parameter
    /// for its popover's overflow behavior. MudBlazor's positioning script decides whether to flip from this class
    /// alone, so adding it restores the flip for selects.
    /// </summary>
    public const string FlipWhenOutOfRoomClass = "mud-popover-overflow-flip-onopen";

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        var classes = PopoverClass?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (!classes.Contains(FlipWhenOutOfRoomClass))
        {
            PopoverClass = string.Join(' ', classes.Append(FlipWhenOutOfRoomClass));
        }
    }
}
