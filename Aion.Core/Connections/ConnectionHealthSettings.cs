namespace Aion.Core.Connections;

public class ConnectionHealthSettings
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan ActivityThreshold { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnableAutoHealthCheck { get; set; } = true;
}
