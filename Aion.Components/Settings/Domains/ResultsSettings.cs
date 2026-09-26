using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for the query results grid.
/// </summary>
public class ResultsSettings : SettingsBase
{
    public const int DefaultRowLimit = 1000;

    public override string SettingsId => "Results";
    public override string DisplayName => "Results";
    public override string Icon => AionIcons.TableView;
    public override int Order => 15;

    [Setting(
        Label = "Rows Shown at First",
        Group = "Display",
        Order = 1,
        Description = "Larger results show this many rows, with Load more for the rest. Exports, copying and Analyze always use every row fetched.")]
    public int RowLimit { get; set; } = DefaultRowLimit;

    /// <summary>
    /// The row limit to apply, so a limit saved as zero or less still shows rows.
    /// </summary>
    public int EffectiveRowLimit => RowLimit > 0 ? RowLimit : DefaultRowLimit;
}
