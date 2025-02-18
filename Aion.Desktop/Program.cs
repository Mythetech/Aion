using Aion.Components;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace Aion.Desktop
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

            appBuilder.Services
                .AddLogging();

            appBuilder.Services.AddHttpClient();

            appBuilder.RootComponents.Add<Components.App>("app");

            appBuilder.Services.AddAionComponents<ConnectionService>();

            var app = appBuilder.Build();

            app.MainWindow
                .SetSize(1920, 1080)
                .SetUseOsDefaultSize(false)
                .SetFullScreen(false)
                .SetLogVerbosity(10)
                .SetTitle("Aion Desktop");

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            app.Run();
        }
    }
}