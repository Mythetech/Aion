namespace Aion.Components.Connections.Secrets;

/// <summary>
/// Whether a connection's password is kept in the active secret store or asked for each time Aion connects.
/// </summary>
public enum PasswordChoice
{
    DontStore,
    Store
}
