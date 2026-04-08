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
using Aion.Components.NativeMenu;
using Aion.Desktop.NativeMenu;
using Hermes.Abstractions;
using Mythetech.Platform.Sdk;
using Velopack;
using Aion.Desktop.Configuration;
using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Infrastructure.Guards;

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
                    builder.AddPlatformErrorReporting();
                });

            appBuilder.Services.AddHttpClient();
            appBuilder.Services.AddPlatformDiagnostics();

            appBuilder.RootComponents.Add<Components.App>("#app");

            // Framework services
            appBuilder.Services.AddDesktopServices(DesktopHost.Hermes);
            appBuilder.Services.AddJsGuards();
            appBuilder.Services.AddPluginFramework();
            appBuilder.Services.AddDesktopAssetLoader();
            appBuilder.Services.AddNativeSecretManager("aion");
            appBuilder.Services.AddOnePasswordSecretManager();
            appBuilder.Services.AddMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);
            appBuilder.Services.AddRuntimeEnvironment(isProd ? DesktopRuntimeEnvironment.Production() : DesktopRuntimeEnvironment.Development());

            appBuilder.Services.AddAionComponents<ConnectionService>();

            // Settings
            appBuilder.Services.AddSettingsStorage<AionSettingsStorage>();
            appBuilder.Services.RegisterSettingsFromAssemblies(
                typeof(ConnectionSettings).Assembly,
                typeof(PluginSettings).Assembly,
                typeof(UpdateSettings).Assembly);

            // Update service
            appBuilder.Services.AddUpdateService(options =>
            {
                var platform = OperatingSystem.IsWindows() ? "windows"
                    : OperatingSystem.IsMacOS() ? "macos"
                    : "linux";
                var channel = OperatingSystem.IsWindows() ? "win"
                    : OperatingSystem.IsMacOS() ? "osx"
                    : "linux";
                options.UpdateUrl = $"{AionDownloadConfiguration.UpdateBaseUrl}/{platform}";
                options.Channel = channel;
            });

            // Async initialization
            appBuilder.Services.AddAsyncInitialization();
            appBuilder.Services.AddInitializationHook<SettingsInitializationHook>();
            appBuilder.Services.AddInitializationHook<CrashReportingHook>();

            appBuilder.Services.AddSingleton<IConnectionStorage, FileConnectionStorage>();
            appBuilder.Services.AddSingleton<IQuerySaveService, FileQuerySaveService>();

            appBuilder.Services.AddTransient<ILinkOpenService, LinkOpenService>();

            // Native menu services
            appBuilder.Services.AddSingleton<INativeMenuService, Desktop.NativeMenu.NativeMenuService>();
            appBuilder.Services.AddSingleton<INativeMenuCommandDispatcher, NativeMenuCommandDispatcher>();

            var app = appBuilder.Build();

            app.RegisterHermesProvider();

            var menuService = app.Services.GetRequiredService<INativeMenuService>();
            menuService.Initialize(app.MainWindow.MenuBar);

            app.Services.UseMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);
            app.Services.UseSettingsFramework();
            app.Services.UsePluginFramework();
            app.Services.UseUpdateService();

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
