namespace Aion.Components.Connections;

/// <summary>
/// Outcome of trying to reach a database server, carrying the driver's error when it failed.
/// </summary>
public record ConnectionResult(bool Success, IReadOnlyList<string> Databases, string? Error, bool TimedOut = false)
{
    public static ConnectionResult Connected(IReadOnlyList<string> databases) => new(true, databases, null);

    public static ConnectionResult Failed(string error, bool timedOut = false) => new(false, [], error, timedOut);

    public static ConnectionResult FromException(Exception exception) => Failed(DescribeException(exception), IsTimeout(exception));

    private static string DescribeException(Exception exception)
    {
        var message = exception.Message;
        var inner = exception.InnerException?.Message;

        // Npgsql and MySql report "Failed to connect to host" and keep the actual reason (refused, DNS) in the inner exception.
        if (!string.IsNullOrWhiteSpace(inner) && !message.Contains(inner, StringComparison.Ordinal))
            return $"{message} ({inner})";

        return message;
    }

    private static bool IsTimeout(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is TimeoutException or OperationCanceledException)
                return true;

            if (current.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
