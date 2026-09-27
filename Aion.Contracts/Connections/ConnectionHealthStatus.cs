namespace Aion.Contracts.Connections;

public enum ConnectionHealthStatus
{
    Unknown,
    Checking,
    Healthy,
    Unhealthy,
    Timeout,

    /// <summary>
    /// The connection uses a password Aion doesn't have, so it waits for one instead of trying to log in.
    /// </summary>
    NeedsPassword
}
