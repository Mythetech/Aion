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
        
        

        snackbar.Add<AionNotificationBar>(parameters, severity, configure =>
        {
            switch (severity)
            {
                case Severity.Error:
                    configure.VisibleStateDuration = 5500;
                    configure.ActionColor = Color.Error;
                    configure.IconColor = Color.Error;
                    break;
                case Severity.Success:
                    configure.ActionColor = Color.Success;
                    configure.IconColor = Color.Success;
                    break;
                case Severity.Warning:
                    configure.ActionColor = Color.Warning;
                    configure.IconColor = Color.Warning;
                    break;
                case Severity.Info:
                    configure.ActionColor = Color.Info;
                    configure.IconColor = Color.Info;
                    break;
                case Severity.Normal:
                    configure.ActionColor = Color.Inherit;
                    configure.IconColor = Color.Inherit;
                    break;
            }
            
            configure.BackgroundBlurred = true;
        });
    }
}