using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for the schema explorer (the connection panel tree view).
/// The "Browser" id is kept so values saved before the rename still load.
/// </summary>
public class BrowserSettings : SettingsBase
{
    public override string SettingsId => "Browser";
    public override string DisplayName => "Schema explorer";
    public override string Icon => AionIcons.SchemaExplorer;
    public override int Order => 13;

    [Setting(
        Label = "Show System Tables",
        Group = "Display",
        Order = 1,
        Description = "Show tables from system schemas (e.g. pg_catalog, information_schema, sys)")]
    public bool ShowSystemTables { get; set; } = false;
}
