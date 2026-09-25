using Aion.Contracts.Database;
using Microsoft.Data.SqlClient;

namespace Aion.Test.Integration;

internal static class SqlServerReadiness
{
    /// <summary>
    /// SQL Server opens its port well before it accepts logins (much later under emulation on arm64),
    /// so a port wait alone lets setup scripts and the first tests fail with "Login failed".
    /// </summary>
    public static async Task WaitForLoginAsync(IDatabaseProvider provider, string connectionString)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);

        while (true)
        {
            try
            {
                await provider.GetDatabasesAsync(connectionString);
                return;
            }
            catch (SqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
