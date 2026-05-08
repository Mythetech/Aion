using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Querying.Commands;
using Aion.Components.RequestContextPanel.Commands;
using Aion.Components.Settings.Commands;
using Aion.Components.Theme;
using Mythetech.Framework.Components.AppContextDrawer.Commands;
using Mythetech.Framework.Components.CommandPalette;
using Mythetech.Framework.Infrastructure.MessageBus;

namespace Aion.Components.CommandPalette;

public sealed class AionCommandProvider : ICommandProvider
{
    private readonly IMessageBus _bus;
    private readonly ConnectionState _connectionState;

    public AionCommandProvider(IMessageBus bus, ConnectionState connectionState)
    {
        _bus = bus;
        _connectionState = connectionState;
    }

    public ValueTask<IReadOnlyList<PaletteCommand>> GetCommandsAsync(
        string query, CancellationToken ct)
    {
        var commands = new List<PaletteCommand>
        {
            new(
                Id: "panel.history",
                Title: "Open History",
                Description: null,
                Icon: AionIcons.History,
                Keywords: ["history", "past", "recent", "log"],
                InvokeAsync: _ => _bus.PublishAsync(new ActivatePanel("history")),
                Group: "Panels"),

            new(
                Id: "action.run-query",
                Title: "Run Query",
                Description: "Ctrl+Enter",
                Icon: AionIcons.Run,
                Keywords: ["run", "execute", "query", "sql"],
                InvokeAsync: _ => _bus.PublishAsync(new RunQuery()),
                Group: "Actions"),

            new(
                Id: "action.new-query",
                Title: "New Query Tab",
                Description: "Ctrl+T",
                Icon: AionIcons.Add,
                Keywords: ["new", "tab", "query", "create"],
                InvokeAsync: _ => _bus.PublishAsync(new CreateQuery()),
                Group: "Actions"),

            new(
                Id: "action.new-connection",
                Title: "New Connection",
                Description: "Ctrl+N",
                Icon: AionIcons.Connection,
                Keywords: ["new", "connection", "database", "connect"],
                InvokeAsync: _ => _bus.PublishAsync(new PromptCreateConnection()),
                Group: "Actions"),

            new(
                Id: "action.save-query",
                Title: "Save Query",
                Description: null,
                Icon: AionIcons.Save,
                Keywords: ["save", "query"],
                InvokeAsync: _ => _bus.PublishAsync(new SaveQuery()),
                Group: "Actions"),

            new(
                Id: "action.save-all",
                Title: "Save All Queries",
                Description: null,
                Icon: AionIcons.Save,
                Keywords: ["save", "all", "queries"],
                InvokeAsync: _ => _bus.PublishAsync(new SaveAllQueries()),
                Group: "Actions"),

            new(
                Id: "action.format-query",
                Title: "Format Query",
                Description: null,
                Icon: AionIcons.Format,
                Keywords: ["format", "indent", "beautify", "pretty"],
                InvokeAsync: _ => _bus.PublishAsync(new FormatQuery()),
                Group: "Actions"),

            new(
                Id: "action.copy-query",
                Title: "Copy Query to Clipboard",
                Description: null,
                Icon: AionIcons.Copy,
                Keywords: ["copy", "clipboard", "query"],
                InvokeAsync: _ => _bus.PublishAsync(new CopyQueryToClipboard()),
                Group: "Actions"),

            new(
                Id: "action.rename-query",
                Title: "Rename Active Query",
                Description: null,
                Icon: AionIcons.Edit,
                Keywords: ["rename", "query", "tab", "name"],
                InvokeAsync: _ => _bus.PublishAsync(new PromptRenameActiveQuery()),
                Group: "Actions"),

            new(
                Id: "export.csv",
                Title: "Export to CSV",
                Description: null,
                Icon: AionIcons.Csv,
                Keywords: ["export", "csv", "download", "results"],
                InvokeAsync: _ => _bus.PublishAsync(new ExportResultsToCsv()),
                Group: "Export"),

            new(
                Id: "export.excel",
                Title: "Export to Excel",
                Description: null,
                Icon: AionIcons.Spreadsheet,
                Keywords: ["export", "excel", "xlsx", "spreadsheet", "download", "results"],
                InvokeAsync: _ => _bus.PublishAsync(new ExportResultsToExcel()),
                Group: "Export"),

            new(
                Id: "export.json",
                Title: "Export to JSON",
                Description: null,
                Icon: AionIcons.Json,
                Keywords: ["export", "json", "download", "results"],
                InvokeAsync: _ => _bus.PublishAsync(new ExportResultsToJson()),
                Group: "Export"),

            new(
                Id: "context.info",
                Title: "Open Info Panel",
                Description: null,
                Icon: AionIcons.Info,
                Keywords: ["info", "information", "details", "metadata"],
                InvokeAsync: _ => _bus.PublishAsync(new OpenRequestContextPanel("Info", "")),
                Group: "Context"),

            new(
                Id: "context.transactions",
                Title: "Open Transactions Panel",
                Description: null,
                Icon: AionIcons.Transaction,
                Keywords: ["transaction", "commit", "rollback", "lock"],
                InvokeAsync: _ => _bus.PublishAsync(new OpenRequestContextPanel("Transactions", "")),
                Group: "Context"),

            new(
                Id: "app.settings",
                Title: "Open Settings",
                Description: null,
                Icon: AionIcons.Settings,
                Keywords: ["settings", "preferences", "configuration", "options"],
                InvokeAsync: _ => _bus.PublishAsync(new OpenSettingsDialog()),
                Group: "Settings"),
        };

        foreach (var connection in _connectionState.Connections)
        {
            commands.Add(new PaletteCommand(
                Id: $"panel.connection.{connection.Id}",
                Title: $"Open {connection.Name}",
                Description: connection.Active ? "Connected" : "Disconnected",
                Icon: AionIcons.Connection,
                Keywords: ["connection", "database", connection.Name.ToLowerInvariant()],
                InvokeAsync: _ => _bus.PublishAsync(new ActivatePanel(connection.Id.ToString())),
                Group: "Connections"));
        }

        return ValueTask.FromResult<IReadOnlyList<PaletteCommand>>(commands);
    }
}
