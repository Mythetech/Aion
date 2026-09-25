using Aion.Components.Settings;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class SettingsStateTests
{
    private readonly ISettingsProvider _provider = Substitute.For<ISettingsProvider>();
    private readonly PluginSettings _plugins = new();
    private readonly SettingsState _sut;

    public SettingsStateTests()
    {
        _provider.GetSettings<PluginSettings>().Returns(_plugins);
        _sut = new SettingsState(_provider);
    }

    [Fact]
    public async Task SetPluginStateAsync_PersistsBeforeRaisingSettingsChanged()
    {
        var persistence = new TaskCompletionSource();
        _provider.NotifySettingsChangedAsync(_plugins).Returns(persistence.Task);
        var changed = false;
        _sut.SettingsChanged += _ => changed = true;
        var initial = _sut.PluginState;

        var pending = _sut.SetPluginStateAsync(!initial);

        pending.IsCompleted.ShouldBeFalse();
        changed.ShouldBeFalse();

        persistence.SetResult();
        await pending;

        changed.ShouldBeTrue();
        _sut.PluginState.ShouldBe(!initial);
        await _provider.Received(1).NotifySettingsChangedAsync(_plugins);
    }

    [Fact]
    public async Task SetPluginStateAsync_SameValue_DoesNothing()
    {
        var changed = false;
        _sut.SettingsChanged += _ => changed = true;

        await _sut.SetPluginStateAsync(_sut.PluginState);

        changed.ShouldBeFalse();
        await _provider.DidNotReceiveWithAnyArgs().NotifySettingsChangedAsync(default!);
    }
}
