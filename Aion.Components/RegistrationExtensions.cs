using Aion.Components.Connections;
using Aion.Components.Connections.Services;
using Aion.Components.ForeignKeys;
using Aion.Components.History;
using Aion.Components.Querying;
using Aion.Components.Search;
using Aion.Components.Settings;
using Aion.Components.Settings.Domains;
using Aion.Components.Shared.Snackbar;
using Aion.Contracts.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components;

public static class RegistrationExtensions
{
    public static IServiceCollection AddAionComponents<TConnectionService>(this IServiceCollection services)
        where TConnectionService : class, IConnectionService
    {
        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
            config.SnackbarConfiguration.PreventDuplicates = true;
            config.SnackbarConfiguration.NewestOnTop = true;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.MaximumOpacity = 80;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 200;
            config.SnackbarConfiguration.ShowTransitionDuration = 100;
            config.PopoverOptions.OverflowBehavior = OverflowBehavior.FlipNever;
        });

        services.AddSingleton<GlobalAppState>();
        services.AddSingleton<ConnectionState>();
        services.AddSingleton<QueryState>();
        services.AddSingleton<HistoryState>();

        services.AddSettingsFramework();
        services.RegisterSettingsFromAssemblies(
            typeof(ConnectionSettings).Assembly,
            typeof(PluginSettings).Assembly);
        services.AddPluginFramework();

        services.AddSingleton<SettingsState>();

        services.AddSingleton<IConnectionService, TConnectionService>();
        services.AddSingleton<IConnectionHealthMonitor, ConnectionHealthMonitor>();
        services.AddScoped<IDatabaseProviderFactory, DatabaseProviderFactory>();

        services.AddScoped<IForeignKeyService, ForeignKeyService>();

        services.AddTransient<SearchService>();
        services.AddSingleton<SqlCompletionService>();

        services.AddFluentUIComponents();

        return services;
    }
}
