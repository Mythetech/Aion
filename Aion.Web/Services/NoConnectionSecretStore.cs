using Aion.Components.Connections.Secrets;

namespace Aion.Web.Services;

/// <summary>
/// The browser has no secret manager, and no in-browser database has a password, so there is nothing to store.
/// </summary>
public class NoConnectionSecretStore : IConnectionSecretStore
{
    private const string Reason = "Passwords can't be stored in the browser";

    public string? ActiveStoreName => null;

    public string? UnavailableReason => Reason;

    public Task<SecretLookup> GetPasswordAsync(string store, Guid connectionId) =>
        Task.FromResult(SecretLookup.Unavailable(Reason));

    public Task<StoreResult> SavePasswordAsync(Guid connectionId, string password, string? previousStore) =>
        Task.FromResult(StoreResult.Failed(null, Reason));

    public Task DeletePasswordAsync(string store, Guid connectionId) => Task.CompletedTask;
}
