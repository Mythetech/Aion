using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for the database object browser (connection panel tree view).
/// </summary>
public class BrowserSettings : SettingsBase
{
    public override string SettingsId => "Browser";
    public override string DisplayName => "Browser";
    public override string Icon => AionIcons.Connection;
    public override int Order => 13;

    [Setting(
        Label = "Show System Tables",
        Group = "Display",
        Order = 1,
        Description = "Show tables from system schemas (e.g. pg_catalog, information_schema, sys)")]
    public bool ShowSystemTables { get; set; } = false;
}
