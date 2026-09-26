using System.Runtime.InteropServices;
using Aion.Components.Connections;
using Aion.Components.Querying;
using Mythetech.Framework.Infrastructure;

namespace Aion.Components.Infrastructure;

public static class BugReportLink
{
    private const string NewIssue = "https://github.com/Mythetech/Aion/issues/new";

    /// <summary>
    /// A new GitHub issue with the details a bug report usually has to ask for filled in, so the reporter
    /// only describes what happened.
    /// </summary>
    public static string Create(string version, string host, string? engine)
    {
        var body = $"""
            **What happened?**


            **What did you expect to happen?**


            **Steps to reproduce**


            ---
            Version: {version}
            Host: {host}
            Engine: {engine ?? "no connection"}
            """;

        return $"{NewIssue}?body={Uri.EscapeDataString(body)}";
    }
}

/// <summary>
/// Opens a pre-filled bug report for the app as it is right now: its version, which host it runs in and the
/// engine of the active tab's connection.
/// </summary>
public class BugReporter
{
    private readonly ILinkOpenService _links;
    private readonly ConnectionState _connections;
    private readonly QueryState _queries;

    public BugReporter(ILinkOpenService links, ConnectionState connections, QueryState queries)
    {
        _links = links;
        _connections = connections;
        _queries = queries;
    }

    public async Task OpenAsync()
    {
        var version = typeof(BugReporter).Assembly.GetName().Version?.ToString() ?? "unknown";
        var host = OperatingSystem.IsBrowser() ? "Web" : $"Desktop ({RuntimeInformation.OSDescription})";
        var connection = _connections.Connections.FirstOrDefault(c => c.Id == _queries.Active?.ConnectionId);
        var engine = connection == null ? null : ConnectionDescription.EngineName(connection.Type);

        await _links.OpenLinkAsync(BugReportLink.Create(version, host, engine));
    }
}
