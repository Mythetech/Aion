namespace Aion.Components.Connections.Secrets;

/// <summary>
/// Where a saved password went, or why it couldn't be saved. On failure <see cref="Store"/> names the store it was
/// meant for, when there was one.
/// </summary>
public sealed record StoreResult(string? Store, string? Error)
{
    public bool Success => Error == null;

    public static StoreResult StoredIn(string store) => new(store, null);

    public static StoreResult Failed(string? store, string error) => new(store, error);
}
