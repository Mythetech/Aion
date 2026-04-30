namespace Aion.Contracts.Connections;

public enum ConnectionHealthStatus
{
    Unknown,
    Checking,
    Healthy,
    Unhealthy,
    Timeout
}
