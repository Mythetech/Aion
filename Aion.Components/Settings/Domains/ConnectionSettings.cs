using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;

namespace Aion.Components.Settings.Domains;

/// <summary>
/// Settings for database connection behavior and health monitoring.
/// </summary>
public class ConnectionSettings : SettingsBase
{
    public override string SettingsId => "Connections";
    public override string DisplayName => "Connections";
    public override string Icon => AionIcons.Connection;
    public override int Order => 12;

    [Setting(
        Label = "Enable Auto Health Check",
        Group = "Health Monitoring",
        Order = 1,
        Description = "Automatically monitor connection health in the background")]
    public bool EnableAutoHealthCheck { get; set; } = true;

    [Setting(
        Label = "Poll Interval (seconds)",
        Group = "Health Monitoring",
        Order = 2,
        Min = 10,
        Max = 300,
        Step = 10,
        Description = "How often to check connection health")]
    public int PollIntervalSeconds { get; set; } = 60;

    [Setting(
        Label = "Activity Threshold (minutes)",
        Group = "Health Monitoring",
        Order = 3,
        Min = 5,
        Max = 120,
        Step = 5,
        Description = "Only check connections active within this time window")]
    public int ActivityThresholdMinutes { get; set; } = 30;

    [Setting(
        Label = "Connection Timeout (seconds)",
        Group = "Health Monitoring",
        Order = 4,
        Min = 1,
        Max = 30,
        Step = 1,
        Description = "Timeout for health check connections")]
    public int ConnectionTimeoutSeconds { get; set; } = 5;

    // Convenience properties for backward compatibility with ConnectionHealthSettings
    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
    public TimeSpan ActivityThreshold => TimeSpan.FromMinutes(ActivityThresholdMinutes);
    public TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
}
