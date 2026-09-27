namespace Aion.Components.Connections.Secrets;

/// <summary>
/// Keeps connection passwords out of the saved connections, in whichever secret manager the host offers. A password
/// is written to the active store and read back from the store the connection recorded, so switching the active
/// store later never strands a password saved earlier.
/// </summary>
public interface IConnectionSecretStore
{
    /// <summary>
    /// The store a password saved now would go to, or null when the active store can't take one.
    /// </summary>
    string? ActiveStoreName { get; }

    /// <summary>
    /// Why a password can't be saved right now, or null when <see cref="ActiveStoreName"/> can take one.
    /// </summary>
    string? UnavailableReason { get; }

    Task<SecretLookup> GetPasswordAsync(string store, Guid connectionId);

    /// <summary>
    /// Saves the password to the active store. When <paramref name="previousStore"/> names another store, the
    /// password is deleted from it once the new copy is written, so a failed write never loses the old one.
    /// </summary>
    Task<StoreResult> SavePasswordAsync(Guid connectionId, string password, string? previousStore);

    Task DeletePasswordAsync(string store, Guid connectionId);
}
