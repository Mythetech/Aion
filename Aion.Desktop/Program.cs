using Aion.Components;
using Aion.Components.Infrastructure;
using Aion.Components.Settings.Domains;
using Mythetech.Framework.Desktop;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;
using Aion.Components.Querying;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Aion.Desktop.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Desktop.Environment;
using Velopack;

namespace Aion.Desktop
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build().Run();
            
            var isProd = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")?.Equals("Production", StringComparison.OrdinalIgnoreCase) ?? false;

            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            appBuilder.Services
                .AddLogging(builder =>
                {
                    builder.AddConfiguration(configuration.GetSection("Logging"));
                    builder.AddConsole();
                });

            appBuilder.Services
                .AddLogging();

            appBuilder.Services.AddHttpClient();

            appBuilder.RootComponents.Add<Components.App>("app");

            // Framework services
            appBuilder.Services.AddDesktopServices();
            appBuilder.Services.AddPluginFramework();
            appBuilder.Services.AddDesktopAssetLoader();
            appBuilder.Services.AddNativeSecretManager("aion");
            appBuilder.Services.AddOnePasswordSecretManager();
            appBuilder.Services.AddMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);
            appBuilder.Services.AddRuntimeEnvironment(isProd ? DesktopRuntimeEnvironment.Production() : DesktopRuntimeEnvironment.Development());

            appBuilder.Services.AddAionComponents<ConnectionService>();

            // Settings storage for framework
            appBuilder.Services.AddSettingsStorage<AionSettingsStorage>();

            appBuilder.Services.AddSingleton<IConnectionStorage, FileConnectionStorage>();
            appBuilder.Services.AddSingleton<IQuerySaveService, FileQuerySaveService>();

            appBuilder.Services.AddSingleton<IPhotinoAppProvider, PhotinoAppProvider>();
            appBuilder.Services.AddTransient<IFileSaveService, PhotinoInteropFileSaveService>();
            appBuilder.Services.AddTransient<ILinkOpenService, LinkOpenService>();

            var app = appBuilder.Build();

            var appProvider = ((PhotinoAppProvider)app.Services.GetRequiredService<IPhotinoAppProvider>());

            appProvider.Instance = app;

            app.Services.UseMessageBus(typeof(Program).Assembly, typeof(Components.App).Assembly);

            // Initialize settings framework
            app.Services
                .RegisterSettings<ConnectionSettings>()
                .RegisterSettings<EditorSettings>()
                .RegisterSettings<PluginSettings>()
                .UseSettingsFramework();

            // Load persisted settings
            app.Services.LoadPersistedSettingsAsync().GetAwaiter().GetResult();

            app.Services.UsePlugins();

            app.MainWindow
                .SetSize(1920, 1080)
                .SetUseOsDefaultSize(false)
                .SetFullScreen(false)
                .SetLogVerbosity(0)
                .SetSmoothScrollingEnabled(true)
                .SetJavascriptClipboardAccessEnabled(true)
                .SetTransparent(true)
                .SetTitle("Aion Desktop");
            
            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };
            
            app.Run();
        }
    }
}