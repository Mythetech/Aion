using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings;

/// <summary>
/// Registers the settings sections each host shows. The framework's assembly scan would also list
/// sections for features a host does not have (such as an MCP server), so sections are added one by one.
/// </summary>
public static class AionSettingsRegistrationExtensions
{
    /// <summary>
    /// Settings shared by every host.
    /// </summary>
    public static IServiceCollection AddAionSettings(this IServiceCollection services)
    {
        services.AddSettingsFramework();
        services.AddSettingsSection<AppearanceSettings>();
        services.AddSettingsSection<EditorSettings>();
        services.AddSettingsSection<BrowserSettings>();
        services.AddSettingsSection<ResultsSettings>();

        // The health monitor reads these on every host, but only the desktop polls real servers,
        // so the section is listed by AddAionDesktopSettings alone.
        services.TryAddSingleton<ConnectionSettings>();

        services.AddSingleton<SettingsState>();
        services.AddSingleton<ThemeState>();

        return services;
    }

    /// <summary>
    /// Settings for features only the desktop host has: health polling of real servers,
    /// plugins, and crash and error reporting.
    /// </summary>
    public static IServiceCollection AddAionDesktopSettings(this IServiceCollection services)
    {
        services.AddSettingsSection<ConnectionSettings>();
        services.AddSettingsSection<PluginSettings>();
        services.AddSettingsSection<PrivacySettings>();

        return services;
    }

    /// <summary>
    /// Registers a settings section so it can be injected, is loaded and saved, and is listed in the Settings dialog.
    /// </summary>
    public static IServiceCollection AddSettingsSection<TSettings>(this IServiceCollection services)
        where TSettings : SettingsBase
    {
        services.TryAddSingleton<TSettings>();
        services.Configure<SettingsRegistrationOptions>(options =>
        {
            if (!options.DiscoveredSettingsTypes.Contains(typeof(TSettings)))
                options.DiscoveredSettingsTypes.Add(typeof(TSettings));
        });

        return services;
    }
}
