using Aion.Components.Connections.Secrets;

namespace Aion.Test.TestDoubles;

/// <summary>
/// An in-memory connection secret store whose active store is "Test Vault" until a test changes it.
/// </summary>
public class ConnectionSecretStoreFake : IConnectionSecretStore
{
    public const string Vault = "Test Vault";

    public string? ActiveStoreName { get; set; } = Vault;

    public string? UnavailableReason { get; set; }

    /// <summary>Stored passwords by store name and connection.</summary>
    public Dictionary<(string Store, Guid ConnectionId), string> Passwords { get; } = [];

    /// <summary>Stores that answer every read with this lookup instead of their passwords.</summary>
    public Dictionary<string, SecretLookup> Lookups { get; } = [];

    /// <summary>When set, saving fails with this error.</summary>
    public string? SaveError { get; set; }

    public List<string> Calls { get; } = [];

    public void MakeReadOnly(string activeStore)
    {
        ActiveStoreName = null;
        UnavailableReason = $"{activeStore} can't store passwords";
        SaveError = UnavailableReason;
    }

    public Task<SecretLookup> GetPasswordAsync(string store, Guid connectionId)
    {
        Calls.Add($"get {store}");

        if (Lookups.TryGetValue(store, out var lookup))
            return Task.FromResult(lookup);

        return Task.FromResult(Passwords.TryGetValue((store, connectionId), out var password)
            ? SecretLookup.Found(password)
            : SecretLookup.NotFound($"The password wasn't found in {store}"));
    }

    public Task<StoreResult> SavePasswordAsync(Guid connectionId, string password, string? previousStore)
    {
        Calls.Add($"save {ActiveStoreName ?? "(none)"} previous {previousStore ?? "(none)"}");

        if (SaveError != null || ActiveStoreName == null)
            return Task.FromResult(StoreResult.Failed(ActiveStoreName, SaveError ?? UnavailableReason ?? "unavailable"));

        Passwords[(ActiveStoreName, connectionId)] = password;
        if (previousStore != null && previousStore != ActiveStoreName)
            Passwords.Remove((previousStore, connectionId));

        return Task.FromResult(StoreResult.StoredIn(ActiveStoreName));
    }

    public Task DeletePasswordAsync(string store, Guid connectionId)
    {
        Calls.Add($"delete {store}");
        Passwords.Remove((store, connectionId));
        return Task.CompletedTask;
    }
}
