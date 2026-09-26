using Aion.Components.CommandPalette;
using Aion.Components.Connections;
using Aion.Components.Connections.Services;
using Aion.Components.ForeignKeys;
using Aion.Components.History;
using Aion.Components.Infrastructure;
using Aion.Components.Querying;
using Aion.Components.Querying.Editing;
using Aion.Components.Querying.Errors;
using Aion.Components.Search;
using Aion.Components.Settings;
using Aion.Components.Shared.Snackbar;
using Aion.Components.Shortcuts;
using Aion.Contracts.Database;
using Aion.Contracts.Queries.Editing;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Mythetech.Framework.Components.CommandPalette;
using Mythetech.Framework.Components.Kbd;
using Mythetech.Framework.Infrastructure.Plugins;

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

        services.AddAionSettings();
        services.AddPluginFramework();

        services.AddSingleton<IConnectionService, TConnectionService>();
        services.AddSingleton<IConnectionHealthMonitor, ConnectionHealthMonitor>();
        services.AddScoped<IDatabaseProviderFactory, DatabaseProviderFactory>();

        // Singleton because bus consumers, which resolve from the root provider, open foreign key rows too.
        services.AddSingleton<IForeignKeyService, ForeignKeyService>();

        services.AddTransient<SearchService>();
        services.AddSingleton<SqlCompletionService>();
        services.AddSingleton<QueryErrorSuggester>();
        services.AddSingleton<BugReporter>();
        services.AddSingleton<ISqlChangeGenerator, SqlChangeGenerator>();
        services.AddTransient<PendingChangesSqlBuilder>();

        services.AddCommandPalette();
        services.AddCommandProvider<AionCommandProvider>();
        services.AddSingleton(sp => AionKeyBindings.For(sp.GetRequiredService<IPlatformDetector>()));

        return services;
    }
}
