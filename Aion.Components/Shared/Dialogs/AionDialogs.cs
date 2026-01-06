using MudBlazor;

namespace Aion.Components.Shared.Dialogs;

public static class AionDialogs
{
    public static DialogOptions CreateDefaultOptions(MaxWidth maxWidth = MaxWidth.Small)
    =>  new DialogOptions 
    { 
        CloseOnEscapeKey = true, 
        BackgroundClass = "aion-dialog",
        MaxWidth = maxWidth,
        FullWidth = true,
        CloseButton = true,
    };
}