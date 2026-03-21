namespace Aion.Components.NativeMenu;

public interface INativeMenuCommandDispatcher
{
    Task HandleMenuItemClickAsync(string itemId);
}
