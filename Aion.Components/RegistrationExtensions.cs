using Aion.Components.Connections;
using Aion.Components.History;
using Aion.Components.Querying;
using Aion.Components.Search;
using Aion.Components.Shared.Snackbar;
using Aion.Core.Database;
using Aion.Core.Database.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Services;

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
            //config.SnackbarConfiguration.RequireInteraction = true;
            config.SnackbarConfiguration.MaximumOpacity = 80;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 200;
            config.SnackbarConfiguration.ShowTransitionDuration = 100;
        });

        services.AddSingleton<GlobalAppState>();
        services.AddSingleton<ConnectionState>();
        services.AddSingleton<QueryState>();
        services.AddSingleton<HistoryState>();

        services.AddSingleton<IConnectionService, TConnectionService>();
        services.AddScoped<IDatabaseProviderFactory, DatabaseProviderFactory>();
        services.AddScoped<IDatabaseProvider, PostgreSqlProvider>();
        services.AddScoped<IDatabaseProvider, MySqlProvider>();
        services.AddScoped<IDatabaseProvider, SqlServerProvider>();

        services.AddTransient<SearchService>();

        services.AddFluentUIComponents();

        return services;
    }
}