using MudBlazor;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for the Monaco query editor.
/// </summary>
public class EditorSettings : SettingsBase
{
    public override string SettingsId => "Editor";
    public override string DisplayName => "Editor";
    public override string Icon => Icons.Material.Filled.Code;
    public override int Order => 14;

    [Setting(
        Label = "Font Size",
        Group = "Font",
        Order = 1,
        Min = 8,
        Max = 32,
        Step = 1,
        Description = "Font size in the query editor")]
    public int FontSize { get; set; } = 14;

    [Setting(
        Label = "Word Wrap",
        Group = "Display",
        Order = 1,
        Description = "Wrap long lines in the query editor")]
    public bool WordWrap { get; set; } = false;

    [Setting(
        Label = "Show Minimap",
        Group = "Display",
        Order = 2,
        Description = "Show a miniature view of the query")]
    public bool ShowMinimap { get; set; } = false;

    [Setting(
        Label = "Show Line Numbers",
        Group = "Display",
        Order = 3,
        Description = "Show line numbers in the editor")]
    public bool ShowLineNumbers { get; set; } = true;
}
