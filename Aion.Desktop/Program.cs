using Aion.Components;
using Aion.Components.Infrastructure;
using Aion.Components.Settings.Domains;
using Hermes;
using Hermes.Blazor;
using Mythetech.Framework.Desktop;
using Mythetech.Framework.Desktop.Hermes;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.Infrastructure.Initialization;
using Aion.Components.Querying;
using Aion.Desktop.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Desktop.Environment;
using Hermes.Abstractions;
using Velopack;

namespace Aion.Desktop
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                VelopackApp.Build().Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Velopack initialization failed: {ex.Message}");
            }

            HermesWindow.Prewarm();

            var isProd = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Equals("Production", StringComparison.OrdinalIgnoreCase) ?? false;

            var appBuilder = HermesBlazorAppBuilder.CreateDefault(args);

            appBuilder.ConfigureWindow(options =>
            {
                options.Title = "Aion";
                options.Width = 1920;
                options.Height = 1080;
                options.CenterOnScreen = true;
                options.DevToolsEnabled = true;
                options.CustomTitleBar = true;
            });

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            appBuilder.Services
                .AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                });

            appBuilder.Services.AddHttpClient();

            appBuilder.RootComponents.Add<Components.App>("#app");

            // Framework services
            appBuilder.Services.AddDesktopServices(DesktopHost.Hermes);
            appBuilder.Services.AddPluginFramework();
            appBuilder.Services.AddDesktopAssetLoader();
            appBuilder.Services.AddNativeSecretManager("aion");
            appBuilder.Services.AddOnePasswordSecretManager();
            appBuilder.Services.AddMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);
            appBuilder.Services.AddRuntimeEnvironment(isProd ? DesktopRuntimeEnvironment.Production() : DesktopRuntimeEnvironment.Development());

            appBuilder.Services.AddAionComponents<ConnectionService>();

            // Settings
            appBuilder.Services.AddSettingsStorage<AionSettingsStorage>();
            appBuilder.Services.RegisterSettingsFromAssembly(typeof(ConnectionSettings).Assembly);
            appBuilder.Services.RegisterSettingsFromAssembly(typeof(PluginSettings).Assembly);

            // Async initialization
            appBuilder.Services.AddAsyncInitialization();
            appBuilder.Services.AddInitializationHook<SettingsInitializationHook>();

            appBuilder.Services.AddSingleton<IConnectionStorage, FileConnectionStorage>();
            appBuilder.Services.AddSingleton<IQuerySaveService, FileQuerySaveService>();

            appBuilder.Services.AddTransient<ILinkOpenService, LinkOpenService>();

            var app = appBuilder.Build();

            app.RegisterHermesProvider();

            app.Services.UseMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);
            app.Services.UseSettingsFramework();
            app.Services.UsePluginFramework();

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.Dialogs.ShowMessage(
                    "Fatal exception",
                    error.ExceptionObject.ToString(),
                    DialogButtons.Ok,
                    DialogIcon.Error);
            };

            app.Run();

            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
