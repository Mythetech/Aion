using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Metrics.Settings;

public class MetricsSettings : SettingsBase
{
    public override string SettingsId => "Metrics";
    public override string DisplayName => "Metrics";
    public override string Icon => AionIcons.Metrics;
    public override int Order => 15;

    [Setting(Label = "Polling Interval (seconds)", Group = "Server Metrics",
        Description = "How often to poll the server for metrics. Minimum 5, maximum 300.", Order = 0)]
    public int PollingIntervalSeconds { get; set; } = 30;

    [Setting(Label = "Max Data Points", Group = "Data Retention",
        Description = "Maximum number of data points to keep per time-series.", Order = 1)]
    public int MaxDataPoints { get; set; } = 200;

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(Math.Clamp(PollingIntervalSeconds, 5, 300));
}
