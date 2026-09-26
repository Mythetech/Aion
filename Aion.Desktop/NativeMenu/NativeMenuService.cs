using System.Threading.Channels;
using Hermes.Menu;
using Microsoft.Extensions.Logging;
using Aion.Components.NativeMenu;
using Aion.Components.Shortcuts;

namespace Aion.Desktop.NativeMenu;

public class NativeMenuService : INativeMenuService
{
    private readonly ILogger<NativeMenuService> _logger;
    private readonly AionKeyBindings _keys;
    private readonly Channel<string> _clickChannel;

    private NativeMenuBar? _menuBar;
    private bool _isInitialized;

    public NativeMenuService(ILogger<NativeMenuService> logger, AionKeyBindings keys)
    {
        _logger = logger;
        _keys = keys;
        _clickChannel = Channel.CreateUnbounded<string>();
    }

    public bool IsActive => _isInitialized;

    public ChannelReader<string> MenuItemClicks => _clickChannel.Reader;

    public void Initialize(object menuBar)
    {
        if (_isInitialized)
            return;

        try
        {
            _menuBar = (NativeMenuBar)menuBar;
            BuildMenuStructure();
            _menuBar.ItemClicked += OnNativeMenuItemClicked;
            _isInitialized = true;
            _logger.LogInformation("Native menus initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize native menus");
        }
    }

    public void SetItemEnabled(string itemId, bool enabled)
    {
        if (_menuBar?.TryGetItem(itemId, out var item) == true && item is not null)
        {
            item.IsEnabled = enabled;
        }
    }

    public void SetItemLabel(string itemId, string label)
    {
        if (_menuBar?.TryGetItem(itemId, out var item) == true && item is not null)
        {
            item.Label = label;
        }
    }

    public void RebuildPluginMenus(IReadOnlyList<NativePluginMenuData> plugins)
    {
        if (_menuBar is null) return;

        try
        {
            _menuBar.RemoveMenu("Plugins");
        }
        catch
        {
            // Menu may not exist yet on first call
        }

        _menuBar.AddMenu("Plugins", menu =>
        {
            foreach (var plugin in plugins)
            {
                menu.AddSubmenu(plugin.PluginName, sub =>
                {
                    foreach (var item in plugin.Items)
                    {
                        sub.AddItem(item.Text, item.ItemId, mi =>
                        {
                            if (item.Disabled) mi.WithEnabled(false);
                        });
                    }

                    if (plugin.Items.Count > 0)
                        sub.AddSeparator();

                    var aboutId = $"{MenuItemIds.ToolsPluginPrefix}{plugin.PluginId}.about";
                    var toggleId = $"{MenuItemIds.ToolsPluginPrefix}{plugin.PluginId}.toggle";
                    sub.AddItem("About...", aboutId);
                    sub.AddItem(plugin.IsEnabled ? "Disable" : "Enable", toggleId);
                });
            }

            if (plugins.Count > 0)
                menu.AddSeparator();

            menu.AddItem("View Plugins", MenuItemIds.ToolsPluginsView);
            menu.AddItem("Open Directory", MenuItemIds.ToolsPluginsOpenDir);
            menu.AddSeparator();
            menu.AddItem("Import Plugin...", MenuItemIds.ToolsPluginsImport);
            menu.AddItem("Import from URL...", MenuItemIds.ToolsPluginsImportUrl);
        });
    }

    private void OnNativeMenuItemClicked(string itemId)
    {
        _logger.LogDebug("Menu item clicked: {ItemId}", itemId);
        _clickChannel.Writer.TryWrite(itemId);
    }

    private static void WithShortcut(NativeMenuItem item, KeyChord? chord)
    {
        if (chord != null)
        {
            item.WithAccelerator(chord.Accelerator);
        }
    }

    private void BuildMenuStructure()
    {
        if (_menuBar is null)
            return;

        // App menu
        _menuBar.AppMenu
            .AddItem("About Aion", MenuItemIds.AionAbout, position: AppMenuPosition.Top)
            .AddSeparator(AppMenuPosition.AfterAbout)
            .AddItem("Settings...", MenuItemIds.AionSettings, item =>
            {
                if (OperatingSystem.IsMacOS())
                    item.WithAccelerator("Cmd+,");
            });

        // File menu
        _menuBar.AddMenu("File", menu =>
        {
            menu.AddSubmenu("New", sub =>
            {
                sub.AddItem("Connection", MenuItemIds.FileNewConnection, item => WithShortcut(item, _keys.NewConnection));
                sub.AddItem("Query", MenuItemIds.FileNewQuery, item => WithShortcut(item, _keys.NewQuery));
            });
            menu.AddSeparator();
            menu.AddItem("Save Query", MenuItemIds.FileSaveQuery, item => WithShortcut(item, _keys.SaveQuery));
            menu.AddItem("Save All Queries", MenuItemIds.FileSaveAllQueries);
            menu.AddItem("Save Query as File", MenuItemIds.FileSaveQueryAs);
            menu.AddSeparator();
            menu.AddSubmenu("Export", sub =>
            {
                sub.AddItem("Export Results to JSON", MenuItemIds.FileExportJson);
                sub.AddItem("Export Results to CSV", MenuItemIds.FileExportCsv);
                sub.AddItem("Export Results to Excel", MenuItemIds.FileExportExcel);
            });
        });

        // Edit menu
        _menuBar.AddMenu("Edit", menu =>
        {
            menu.AddItem("Copy Query", MenuItemIds.EditCopyQuery, item => WithShortcut(item, _keys.CopyQuery));
            menu.AddItem("Rename Query", MenuItemIds.EditRenameQuery);
            menu.AddItem("Format", MenuItemIds.EditFormat);
            menu.AddSeparator();
            menu.AddItem("Edit Mode", MenuItemIds.EditMode);
        });

        // Tools menu
        _menuBar.AddMenu("Tools", menu =>
        {
            menu.AddSubmenu("History", sub =>
            {
                sub.AddItem("View History", MenuItemIds.ToolsHistoryView);
                sub.AddItem("Clear", MenuItemIds.ToolsHistoryClear);
            });
            menu.AddItem("Secret Manager", MenuItemIds.ToolsSecrets);
        });

        // Plugins menu (top-level, rebuilt dynamically)
        _menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View Plugins", MenuItemIds.ToolsPluginsView);
            menu.AddItem("Import Plugin...", MenuItemIds.ToolsPluginsImport);
        });

        // Help menu
        _menuBar.AddMenu("Help", menu =>
        {
            menu.AddItem("Help", MenuItemIds.Help);
        });
    }
}
