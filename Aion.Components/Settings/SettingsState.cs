using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings;

/// <summary>
/// Adapter class that provides backward compatibility with existing code
/// while delegating to the new framework settings infrastructure.
/// </summary>
public class SettingsState
{
    private readonly ISettingsProvider _provider;

    // Cached reference for performance
    private PluginSettings? _pluginSettings;

    public event Action<SettingsState>? SettingsChanged;

    public SettingsState(ISettingsProvider provider)
    {
        _provider = provider;
    }

    private PluginSettings Plugins => _pluginSettings ??= _provider.GetSettings<PluginSettings>()!;

    /// <summary>
    /// Whether the plugin menu is enabled. Delegates to framework's PluginSettings.PluginsActive.
    /// </summary>
    public bool PluginState
    {
        get => Plugins.PluginsActive;
        set
        {
            if (Plugins.PluginsActive == value) return;
            Plugins.PluginsActive = value;
            Plugins.MarkDirty();
            _ = _provider.NotifySettingsChangedAsync(Plugins);
            SettingsChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets a specific settings model by type.
    /// </summary>
    public T? GetSettings<T>() where T : SettingsBase => _provider.GetSettings<T>();

    /// <summary>
    /// Notifies listeners that settings have changed.
    /// Called after the settings dialog commits changes.
    /// </summary>
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke(this);
    }
}
