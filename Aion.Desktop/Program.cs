﻿using Aion.Components;
using Aion.Components.Infrastructure;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Aion.Desktop.Services;
using Velopack;

namespace Aion.Desktop
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build().Run();
            
            var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

            appBuilder.Services
                .AddLogging();

            appBuilder.Services.AddHttpClient();

            appBuilder.RootComponents.Add<Components.App>("app");
            
            appBuilder.Services.AddMessageBus(typeof(Program).Assembly, typeof(IConsumer<>).Assembly);

            appBuilder.Services.AddAionComponents<ConnectionService>();
            
            appBuilder.Services.AddSingleton<IConnectionStorage, FileConnectionStorage>();
            appBuilder.Services.AddSingleton<IQuerySaveService, FileQuerySaveService>();

            appBuilder.Services.AddSingleton<IPhotinoAppProvider, PhotinoAppProvider>();
            appBuilder.Services.AddTransient<IFileSaveService, PhotinoInteropFileSaveService>();
            appBuilder.Services.AddTransient<ILinkOpenService, LinkOpenService>();
            
            var app = appBuilder.Build();

            var appProvider = ((PhotinoAppProvider)app.Services.GetRequiredService<IPhotinoAppProvider>());

            appProvider.Instance = app;
            
            app.Services.UseMessageBus(typeof(Program).Assembly, typeof(IConsumer<>).Assembly);

            app.MainWindow
                .SetSize(1920, 1080)
                .SetUseOsDefaultSize(false)
                .SetFullScreen(false)
                .SetLogVerbosity(10)
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