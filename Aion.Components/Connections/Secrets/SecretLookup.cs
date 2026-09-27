namespace Aion.Components.Connections.Secrets;

public enum SecretLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

/// <summary>
/// A password read from a store, or why none came back.
/// </summary>
public sealed record SecretLookup(SecretLookupStatus Status, string? Password, string? Reason)
{
    public bool IsFound => Status == SecretLookupStatus.Found && Password != null;

    public static SecretLookup Found(string password) => new(SecretLookupStatus.Found, password, null);

    public static SecretLookup NotFound(string reason) => new(SecretLookupStatus.NotFound, null, reason);

    public static SecretLookup Unavailable(string reason) => new(SecretLookupStatus.Unavailable, null, reason);
}
