using MudBlazor;

namespace Aion.Components.Shared.Snackbar;

public static class SnackbarExtensions
{
    public static void AddAionNotification(this ISnackbar snackbar, string message, Severity severity = Severity.Info)
    {
        var parameters = new Dictionary<string, object>
        {
            { "Message", message },
            {"Severity", severity}
        };

        snackbar.Add<AionNotificationBar>(parameters, severity);
    }
}