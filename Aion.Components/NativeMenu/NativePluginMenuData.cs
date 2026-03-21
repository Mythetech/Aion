namespace Aion.Components.NativeMenu;

public record NativePluginMenuData(
    string PluginId,
    string PluginName,
    bool IsEnabled,
    IReadOnlyList<NativePluginMenuItemData> Items);

public record NativePluginMenuItemData(
    string ItemId,
    string Text,
    bool Disabled);
