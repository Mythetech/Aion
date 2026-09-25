using Aion.Components.Settings.Domains;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Theme;

/// <summary>
/// The chosen theme mode and the operating system's color preference, resolved to light or dark.
/// The mode is persisted through <see cref="AppearanceSettings"/>.
/// </summary>
public class ThemeState
{
    private readonly AppearanceSettings _settings;
    private readonly ISettingsProvider _settingsProvider;
    private ThemeMode _mode;
    private bool _systemPrefersDark;

    public ThemeState(AppearanceSettings settings, ISettingsProvider settingsProvider)
    {
        _settings = settings;
        _settingsProvider = settingsProvider;
        _mode = settings.Theme;
    }

    public event Action? ThemeChanged;

    public ThemeMode Mode => _mode;

    public bool FollowsSystem => _mode == ThemeMode.System;

    public bool IsDarkMode => Resolve(_mode, _systemPrefersDark);

    public static bool Resolve(ThemeMode mode, bool systemPrefersDark) => mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => systemPrefersDark,
    };

    /// <summary>
    /// Chooses a theme mode and persists it.
    /// </summary>
    public async Task SetModeAsync(ThemeMode mode)
    {
        if (_mode == mode && _settings.Theme == mode)
            return;

        _settings.Theme = mode;
        Update(mode, _systemPrefersDark);
        await _settingsProvider.NotifySettingsChangedAsync(_settings);
    }

    /// <summary>
    /// Switches to the opposite of what is on screen, as an explicit light or dark choice.
    /// </summary>
    public Task ToggleAsync() => SetModeAsync(IsDarkMode ? ThemeMode.Light : ThemeMode.Dark);

    /// <summary>
    /// Adopts a mode that was loaded from storage or saved from the settings dialog.
    /// </summary>
    public void ApplySettings(AppearanceSettings settings) => Update(settings.Theme, _systemPrefersDark);

    /// <summary>
    /// Records the operating system's color preference, which decides the theme in <see cref="ThemeMode.System"/>.
    /// </summary>
    public void SetSystemPreference(bool prefersDark) => Update(_mode, prefersDark);

    private void Update(ThemeMode mode, bool systemPrefersDark)
    {
        var previousMode = _mode;
        var wasDark = IsDarkMode;

        _mode = mode;
        _systemPrefersDark = systemPrefersDark;

        if (previousMode != _mode || wasDark != IsDarkMode)
            ThemeChanged?.Invoke();
    }
}
