using Aion.Components.Settings.Editors;
using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for how Aion looks. The theme is also changed from the rail, through <see cref="ThemeState"/>.
/// </summary>
public class AppearanceSettings : SettingsBase
{
    public override string SettingsId => "Appearance";
    public override string DisplayName => "Appearance";
    public override string Icon => AionIcons.Appearance;
    public override int Order => 10;

    [Setting(
        Label = "Theme",
        Group = "Theme",
        Order = 1,
        Description = "System follows your operating system's light or dark setting",
        CustomEditor = typeof(ThemeModeSettingEditor))]
    public ThemeMode Theme { get; set; } = ThemeMode.System;
}
