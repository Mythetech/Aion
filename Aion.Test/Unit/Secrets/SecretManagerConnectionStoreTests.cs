using Aion.Components.Connections.Secrets;
using Aion.Desktop.Services;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure.Secrets;
using Shouldly;

namespace Aion.Test.Unit.Secrets;

public class SecretManagerConnectionStoreTests
{
    private readonly Guid _connectionId = Guid.NewGuid();
    private readonly List<string> _log = [];
    private readonly SecretManagerState _state = new();

    private string Key => $"Aion connection {_connectionId}";

    private SecretManagerConnectionStore CreateStore() =>
        new(_state, NullLogger<SecretManagerConnectionStore>.Instance);

    private FakeWritableSecretManager Keychain()
    {
        var manager = new FakeWritableSecretManager("macOS Keychain", _log);
        _state.RegisterManager(manager);
        return manager;
    }

    private FakeSecretManager OnePassword()
    {
        var manager = new FakeSecretManager("1Password CLI", _log);
        _state.RegisterManager(manager);
        return manager;
    }

    [Fact]
    public void ActiveStore_WritableManager_IsNamedAndAvailable()
    {
        Keychain();

        var store = CreateStore();

        store.ActiveStoreName.ShouldBe("macOS Keychain");
        store.UnavailableReason.ShouldBeNull();
    }

    [Fact]
    public void ActiveStore_ReadOnlyManager_IsUnavailableWithItsName()
    {
        Keychain();
        _state.SetActiveManager(OnePassword());

        var store = CreateStore();

        store.ActiveStoreName.ShouldBeNull();
        store.UnavailableReason.ShouldBe("1Password CLI can't store passwords");
    }

    [Fact]
    public void ActiveStore_NoManagers_IsUnavailable()
    {
        var store = CreateStore();

        store.ActiveStoreName.ShouldBeNull();
        store.UnavailableReason.ShouldBe("No secret manager is available");
    }

    [Fact]
    public async Task Save_WritesToTheActiveManagerUnderTheConnectionsKey()
    {
        var keychain = Keychain();

        var result = await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: null);

        result.ShouldBe(StoreResult.StoredIn("macOS Keychain"));
        keychain.Secrets[Key].ShouldBe("hunter2");
    }

    [Fact]
    public async Task Save_ReadOnlyActiveManager_IsRefused()
    {
        var keychain = Keychain();
        _state.SetActiveManager(OnePassword());

        var result = await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: null);

        result.Success.ShouldBeFalse();
        result.Store.ShouldBe("1Password CLI");
        result.Error.ShouldBe("1Password CLI can't store passwords");
        keychain.Secrets.ShouldBeEmpty();
    }

    [Fact]
    public async Task Save_ManagerFails_ReportsItsError()
    {
        var keychain = Keychain();
        keychain.Failure = SecretOperationResult.Fail("The keychain is locked.", SecretOperationErrorKind.AccessDenied);

        var result = await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: null);

        result.ShouldBe(StoreResult.Failed("macOS Keychain", "The keychain is locked."));
    }

    [Fact]
    public async Task Save_WithAPreviousStore_WritesTheNewCopyThenDeletesTheOld()
    {
        var old = new FakeWritableSecretManager("Old Vault", _log);
        _state.RegisterManager(old);
        old.Secrets[Key] = "hunter2";
        var keychain = Keychain();
        _state.SetActiveManager(keychain);

        var result = await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: "Old Vault");

        result.ShouldBe(StoreResult.StoredIn("macOS Keychain"));
        keychain.Secrets[Key].ShouldBe("hunter2");
        old.Secrets.ShouldBeEmpty();
        _log.ShouldBe([$"set macOS Keychain {Key}", $"delete Old Vault {Key}"]);
    }

    [Fact]
    public async Task Save_WithAPreviousStore_KeepsTheOldCopyWhenTheWriteFails()
    {
        var old = new FakeWritableSecretManager("Old Vault", _log);
        _state.RegisterManager(old);
        old.Secrets[Key] = "hunter2";
        var keychain = Keychain();
        _state.SetActiveManager(keychain);
        keychain.Failure = SecretOperationResult.Fail("The keychain is locked.", SecretOperationErrorKind.AccessDenied);

        var result = await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: "Old Vault");

        result.Success.ShouldBeFalse();
        old.Secrets[Key].ShouldBe("hunter2");
        _log.ShouldNotContain(entry => entry.StartsWith("delete"));
    }

    [Fact]
    public async Task Save_PreviousStoreIsTheActiveOne_DeletesNothing()
    {
        Keychain();

        await CreateStore().SavePasswordAsync(_connectionId, "hunter2", previousStore: "macOS Keychain");

        _log.ShouldBe([$"set macOS Keychain {Key}"]);
    }

    [Fact]
    public async Task Get_ReadsFromTheRecordedManagerWhateverIsActive()
    {
        var keychain = Keychain();
        keychain.Secrets[Key] = "hunter2";
        _state.SetActiveManager(OnePassword());

        var lookup = await CreateStore().GetPasswordAsync("macOS Keychain", _connectionId);

        lookup.ShouldBe(SecretLookup.Found("hunter2"));
    }

    [Fact]
    public async Task Get_MissingSecret_IsNotFound()
    {
        Keychain();

        var lookup = await CreateStore().GetPasswordAsync("macOS Keychain", _connectionId);

        lookup.Status.ShouldBe(SecretLookupStatus.NotFound);
        lookup.Reason.ShouldBe("The password wasn't found in macOS Keychain");
    }

    [Fact]
    public async Task Get_ManagerNoLongerRegistered_IsUnavailable()
    {
        Keychain();

        var lookup = await CreateStore().GetPasswordAsync("1Password CLI", _connectionId);

        lookup.Status.ShouldBe(SecretLookupStatus.Unavailable);
        lookup.Reason.ShouldBe("1Password CLI isn't available");
    }

    [Fact]
    public async Task Get_ManagerFails_IsUnavailableWithItsError()
    {
        var onePassword = OnePassword();
        onePassword.Failure = SecretOperationResult.Fail("You are not signed in.", SecretOperationErrorKind.AccessDenied);

        var lookup = await CreateStore().GetPasswordAsync("1Password CLI", _connectionId);

        lookup.Status.ShouldBe(SecretLookupStatus.Unavailable);
        lookup.Reason.ShouldBe("1Password CLI couldn't read the password: You are not signed in.");
    }

    [Fact]
    public async Task Delete_RemovesThePasswordFromTheNamedManager()
    {
        var keychain = Keychain();
        keychain.Secrets[Key] = "hunter2";
        _state.SetActiveManager(OnePassword());

        await CreateStore().DeletePasswordAsync("macOS Keychain", _connectionId);

        keychain.Secrets.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_NothingToDeleteOrManagerCantWrite_DoesNotThrow()
    {
        Keychain();
        OnePassword();
        var store = CreateStore();

        await Should.NotThrowAsync(() => store.DeletePasswordAsync("macOS Keychain", _connectionId));
        await Should.NotThrowAsync(() => store.DeletePasswordAsync("1Password CLI", _connectionId));
        await Should.NotThrowAsync(() => store.DeletePasswordAsync("Gone", _connectionId));
    }
}
