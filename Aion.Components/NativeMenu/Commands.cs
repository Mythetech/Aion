namespace Aion.Components.NativeMenu;

public record ClearHistory();
public record OpenPluginDirectory();
public record ImportPlugin();
public record ShowAboutPlugin(string PluginId);
public record TogglePlugin(string PluginId);
