using Aion.Components.Connections.Secrets;
using Aion.Web.Services;
using Shouldly;

namespace Aion.Test.Web.Services;

public class NoConnectionSecretStoreTests
{
    private readonly NoConnectionSecretStore _store = new();

    [Fact]
    public void HasNoStoreToOffer()
    {
        _store.ActiveStoreName.ShouldBeNull();
        _store.UnavailableReason.ShouldBe("Passwords can't be stored in the browser");
    }

    [Fact]
    public async Task Save_IsRefused()
    {
        var result = await _store.SavePasswordAsync(Guid.NewGuid(), "hunter2", previousStore: null);

        result.Success.ShouldBeFalse();
        result.Store.ShouldBeNull();
    }

    [Fact]
    public async Task Get_IsUnavailable()
    {
        var lookup = await _store.GetPasswordAsync("macOS Keychain", Guid.NewGuid());

        lookup.Status.ShouldBe(SecretLookupStatus.Unavailable);
    }

    [Fact]
    public async Task Delete_DoesNothing()
    {
        await Should.NotThrowAsync(() => _store.DeletePasswordAsync("macOS Keychain", Guid.NewGuid()));
    }
}
