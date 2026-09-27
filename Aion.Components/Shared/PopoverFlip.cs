namespace Aion.Components.Shared;

/// <summary>
/// Aion turns popover flipping off app-wide (see <c>RegistrationExtensions</c>), so a dropdown or menu near the
/// bottom of the window would open below the fold. MudSelect and MudMenu have no parameter for their popover's
/// overflow behavior, but MudBlazor's positioning script decides whether to flip from this class alone, so adding it
/// to a popover's classes restores the flip for that popover.
/// </summary>
internal static class PopoverFlip
{
    public const string WhenOutOfRoomClass = "mud-popover-overflow-flip-onopen";

    /// <summary>
    /// The popover classes a caller passed, with the flip added once.
    /// </summary>
    public static string AddTo(string? classes)
    {
        var names = classes?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return names.Contains(WhenOutOfRoomClass) ? classes! : string.Join(' ', names.Append(WhenOutOfRoomClass));
    }
}
