using MudBlazor;

namespace Aion.Components.Shared;

public static class AionDialogs
{
    public static DialogOptions CreateDefaultOptions(MaxWidth maxWidth = MaxWidth.Small)
    =>  new DialogOptions 
    { 
        CloseOnEscapeKey = true, 
        BackgroundClass = "aion-dialog",
        MaxWidth = maxWidth,
        FullWidth = true
    };
}