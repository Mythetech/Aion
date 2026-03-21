using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Plugins.Components;
using Mythetech.Framework.Components.Secrets;
using Aion.Components.NativeMenu;
using Aion.Components.AppContextPanel.Commands;
using Aion.Components.Connections.Commands;
using Aion.Components.Querying.Commands;
using Aion.Components.History;
using Aion.Components.Settings;
using Aion.Components.Shared.Dialogs;
using Aion.Components.Shared.Dialogs.Commands;

namespace Aion.Desktop.NativeMenu;

public class NativeMenuCommandDispatcher : INativeMenuCommandDispatcher
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NativeMenuCommandDispatcher> _logger;

    private readonly Dictionary<string, Func<Task>> _handlers;

    public NativeMenuCommandDispatcher(
        IMessageBus messageBus,
        ILogger<NativeMenuCommandDispatcher> logger)
    {
        _messageBus = messageBus;
        _logger = logger;

        _handlers = new Dictionary<string, Func<Task>>
        {
            // App menu
            [MenuItemIds.AionAbout] = () => _messageBus.PublishAsync(new ShowHelp()),
            [MenuItemIds.AionSettings] = () => ShowDialog<SettingsDialog>("Settings", MaxWidth.Large),

            // File menu
            [MenuItemIds.FileNewConnection] = () => _messageBus.PublishAsync(new PromptCreateConnection()),
            [MenuItemIds.FileNewQuery] = () => _messageBus.PublishAsync(new CreateQuery()),
            [MenuItemIds.FileSaveQuery] = () => _messageBus.PublishAsync(new SaveQuery()),
            [MenuItemIds.FileSaveAllQueries] = () => _messageBus.PublishAsync(new SaveAllQueries()),
            [MenuItemIds.FileSaveQueryAs] = () => _messageBus.PublishAsync(new SaveQueryAs()),
            [MenuItemIds.FileExportJson] = () => _messageBus.PublishAsync(new ExportResultsToJson()),
            [MenuItemIds.FileExportCsv] = () => _messageBus.PublishAsync(new ExportResultsToCsv()),
            [MenuItemIds.FileExportExcel] = () => _messageBus.PublishAsync(new ExportResultsToExcel()),

            // Edit menu
            [MenuItemIds.EditCopyQuery] = () => _messageBus.PublishAsync(new CopyQueryToClipboard()),
            [MenuItemIds.EditRenameQuery] = () => _messageBus.PublishAsync(new PromptRenameActiveQuery()),
            [MenuItemIds.EditFormat] = () => _messageBus.PublishAsync(new FormatQuery()),
            [MenuItemIds.EditMode] = () => _messageBus.PublishAsync(new EnableEditModeFromQuery()),

            // Tools menu
            [MenuItemIds.ToolsHistoryView] = () => _messageBus.PublishAsync(new OpenHistoryPanel()),
            [MenuItemIds.ToolsHistoryClear] = () => _messageBus.PublishAsync(new ClearHistory()),
            [MenuItemIds.ToolsSecrets] = () => ShowDialog<SecretManagerDialog>("Secret Manager", MaxWidth.Large, fullWidth: true),

            // Plugins
            [MenuItemIds.ToolsPluginsView] = () => ShowDialog<PluginManagementDialog>("Plugin Management", MaxWidth.Large, fullWidth: true),
            [MenuItemIds.ToolsPluginsOpenDir] = () => _messageBus.PublishAsync(new OpenPluginDirectory()),
            [MenuItemIds.ToolsPluginsImport] = () => _messageBus.PublishAsync(new ImportPlugin()),
            [MenuItemIds.ToolsPluginsImportUrl] = () => ShowDialog<DefaultPluginUrlLoadDialog>("Import Plugin from URL", MaxWidth.Small),

            // Help
            [MenuItemIds.Help] = () => _messageBus.PublishAsync(new ShowHelp()),
        };
    }

    public async Task HandleMenuItemClickAsync(string itemId)
    {
        try
        {
            if (_handlers.TryGetValue(itemId, out var handler))
            {
                await handler();
                return;
            }

            // Dynamic: plugin actions
            if (itemId.StartsWith(MenuItemIds.ToolsPluginPrefix))
            {
                var suffix = itemId[MenuItemIds.ToolsPluginPrefix.Length..];

                if (suffix.EndsWith(".about"))
                {
                    var pluginId = suffix[..^".about".Length];
                    await _messageBus.PublishAsync(new ShowAboutPlugin(pluginId));
                    return;
                }

                if (suffix.EndsWith(".toggle"))
                {
                    var pluginId = suffix[..^".toggle".Length];
                    await _messageBus.PublishAsync(new TogglePlugin(pluginId));
                    return;
                }
            }

            _logger.LogWarning("Unhandled menu item click: {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle menu item click: {ItemId}", itemId);
        }
    }

    private Task ShowDialog<TDialog>(string title, MaxWidth maxWidth = MaxWidth.Medium, bool fullWidth = false)
        where TDialog : ComponentBase
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            BackgroundClass = "aion-dialog",
            MaxWidth = maxWidth,
            FullWidth = fullWidth
        };

        return _messageBus.PublishAsync(new ShowDialog(typeof(TDialog), title, options));
    }
}
