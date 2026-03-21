using System.Threading.Channels;

namespace Aion.Components.NativeMenu;

public interface INativeMenuService
{
    bool IsActive { get; }

    void Initialize(object menuBar);

    void SetItemEnabled(string itemId, bool enabled);

    void SetItemLabel(string itemId, string label);

    ChannelReader<string> MenuItemClicks { get; }

    void RebuildPluginMenus(IReadOnlyList<NativePluginMenuData> plugins);
}
