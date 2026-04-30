using System.Threading.Channels;
using Aion.Components.NativeMenu;

namespace Aion.Web.Services;

public class NoOpNativeMenuService : INativeMenuService
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public bool IsActive => false;

    public void Initialize(object menuBar) { }

    public void SetItemEnabled(string itemId, bool enabled) { }

    public void SetItemLabel(string itemId, string label) { }

    public ChannelReader<string> MenuItemClicks => _channel.Reader;

    public void RebuildPluginMenus(IReadOnlyList<NativePluginMenuData> plugins) { }
}
