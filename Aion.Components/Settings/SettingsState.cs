namespace Aion.Components.Settings;

public class SettingsState
{
    public event Action<SettingsState>? SettingsChanged;

    private bool _pluginState = false;

    public bool PluginState
    {
        get => _pluginState;
        set
        {
            if (_pluginState == value) return;
            _pluginState = value;
            SettingsChanged?.Invoke(this);
        }
    }
}
