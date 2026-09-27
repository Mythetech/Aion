using Aion.Components.Connections.Secrets;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.Secrets;

namespace Aion.Desktop.Services;

/// <summary>
/// Keeps connection passwords in the secret managers chosen in Tools > Secret Manager. Managers are called
/// directly rather than through <see cref="SecretManagerState.GetSecretAsync"/>, which caches what it reads and
/// would list connection passwords in the Secret Manager dialog.
/// </summary>
public class SecretManagerConnectionStore : IConnectionSecretStore
{
    private readonly SecretManagerState _state;
    private readonly ILogger<SecretManagerConnectionStore> _logger;

    public SecretManagerConnectionStore(SecretManagerState state, ILogger<SecretManagerConnectionStore> logger)
    {
        _state = state;
        _logger = logger;
    }

    // The id keeps the key stable across renames, and the prefix makes it recognizable in a 1Password item list.
    private static string KeyFor(Guid connectionId) => $"Aion connection {connectionId}";

    public string? ActiveStoreName => _state.CurrentManager is ISecretWriter ? _state.CurrentManager.Name : null;

    public string? UnavailableReason => _state.CurrentManager switch
    {
        null => "No secret manager is available",
        ISecretWriter => null,
        var manager => $"{manager.Name} can't store passwords"
    };

    public async Task<SecretLookup> GetPasswordAsync(string store, Guid connectionId)
    {
        var manager = Find(store);
        if (manager == null)
            return SecretLookup.Unavailable($"{store} isn't available");

        try
        {
            var result = await manager.GetSecretAsync(KeyFor(connectionId));

            if (result is { Success: true, Value.Value: { } password })
                return SecretLookup.Found(password);

            return result.ErrorKind == SecretOperationErrorKind.NotFound
                ? SecretLookup.NotFound($"The password wasn't found in {store}")
                : SecretLookup.Unavailable($"{store} couldn't read the password: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the password of connection {ConnectionId} from {Store}", connectionId, store);
            return SecretLookup.Unavailable($"{store} couldn't read the password: {ex.Message}");
        }
    }

    public async Task<StoreResult> SavePasswordAsync(Guid connectionId, string password, string? previousStore)
    {
        var manager = _state.CurrentManager;
        if (manager is not ISecretWriter writer)
            return StoreResult.Failed(manager?.Name, UnavailableReason!);

        try
        {
            var result = await writer.SetSecretAsync(KeyFor(connectionId), password);
            if (!result.Success)
                return StoreResult.Failed(manager.Name, result.ErrorMessage ?? "The secret manager gave no reason");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not store the password of connection {ConnectionId} in {Store}", connectionId, manager.Name);
            return StoreResult.Failed(manager.Name, ex.Message);
        }

        // Only once the new copy is written, so a failed move leaves the password where it was.
        if (previousStore != null && !IsNamed(manager, previousStore))
            await DeletePasswordAsync(previousStore, connectionId);

        return StoreResult.StoredIn(manager.Name);
    }

    public async Task DeletePasswordAsync(string store, Guid connectionId)
    {
        if (Find(store) is not ISecretWriter writer)
        {
            _logger.LogWarning("The password of connection {ConnectionId} could not be deleted: {Store} can't delete passwords", connectionId, store);
            return;
        }

        try
        {
            var result = await writer.DeleteSecretAsync(KeyFor(connectionId));
            if (!result.Success && result.ErrorKind != SecretOperationErrorKind.NotFound)
            {
                _logger.LogWarning("Could not delete the password of connection {ConnectionId} from {Store}: {Error}",
                    connectionId, store, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete the password of connection {ConnectionId} from {Store}", connectionId, store);
        }
    }

    private ISecretManager? Find(string store) => _state.AvailableManagers.FirstOrDefault(m => IsNamed(m, store));

    private static bool IsNamed(ISecretManager manager, string name) =>
        manager.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
}
