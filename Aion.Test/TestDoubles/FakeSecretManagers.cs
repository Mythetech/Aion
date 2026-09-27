using Mythetech.Framework.Infrastructure.Secrets;

namespace Aion.Test.TestDoubles;

/// <summary>
/// An in-memory secret manager, so no test touches the real keychain or 1Password. Managers sharing one
/// <see cref="Log"/> record the order of writes and deletes across them.
/// </summary>
public class FakeSecretManager : ISecretManager
{
    private readonly List<string> _log;

    public FakeSecretManager(string name, List<string>? log = null)
    {
        Name = name;
        _log = log ?? [];
    }

    public string Name { get; }

    public Dictionary<string, string> Secrets { get; } = [];

    public IReadOnlyList<string> Log => _log;

    /// <summary>When set, every call fails with this error instead of reaching the secrets.</summary>
    public SecretOperationResult? Failure { get; set; }

    public Task<SecretOperationResult<Secret>> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        _log.Add($"get {Name} {key}");

        if (Failure != null)
            return Task.FromResult(SecretOperationResult<Secret>.Fail(Failure.ErrorMessage!, Failure.ErrorKind!.Value));

        return Task.FromResult(Secrets.TryGetValue(key, out var value)
            ? SecretOperationResult<Secret>.Ok(new Secret { Key = key, Value = value })
            : SecretOperationResult<Secret>.Fail($"'{key}' was not found.", SecretOperationErrorKind.NotFound));
    }

    public Task<SecretOperationResult> TestConnectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SecretOperationResult.Ok());

    protected Task<SecretOperationResult> Write(string key, string value)
    {
        _log.Add($"set {Name} {key}");
        if (Failure != null)
            return Task.FromResult(Failure);

        Secrets[key] = value;
        return Task.FromResult(SecretOperationResult.Ok());
    }

    protected Task<SecretOperationResult> Remove(string key)
    {
        _log.Add($"delete {Name} {key}");
        if (Failure != null)
            return Task.FromResult(Failure);

        return Task.FromResult(Secrets.Remove(key)
            ? SecretOperationResult.Ok()
            : SecretOperationResult.Fail($"'{key}' was not found.", SecretOperationErrorKind.NotFound));
    }
}

public class FakeWritableSecretManager : FakeSecretManager, ISecretWriter
{
    public FakeWritableSecretManager(string name, List<string>? log = null) : base(name, log)
    {
    }

    public Task<SecretOperationResult> SetSecretAsync(string key, string value, CancellationToken cancellationToken = default) =>
        Write(key, value);

    public Task<SecretOperationResult> DeleteSecretAsync(string key, CancellationToken cancellationToken = default) =>
        Remove(key);
}
