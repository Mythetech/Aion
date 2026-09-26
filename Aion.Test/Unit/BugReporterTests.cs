using Aion.Components.Connections;
using Aion.Components.Infrastructure;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Mythetech.Framework.Infrastructure;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class BugReporterTests
{
    private readonly ILinkOpenService _links = Substitute.For<ILinkOpenService>();
    private readonly ConnectionState _connections = new(
        Substitute.For<IConnectionService>(), Substitute.For<IDatabaseProviderFactory>(),
        Substitute.For<IMessageBus>(), NullLogger<ConnectionState>.Instance);
    private readonly QueryState _queries = new(Substitute.For<IMessageBus>(), Substitute.For<IQuerySaveService>());

    [Fact]
    public void Link_OpensANewIssueWithTheVersionHostAndEngineInTheBody()
    {
        var link = BugReportLink.Create("1.4.2.0", "Web", "PostgreSQL");

        link.ShouldStartWith("https://github.com/Mythetech/Aion/issues/new?body=");
        var body = Uri.UnescapeDataString(link[(link.IndexOf("?body=", StringComparison.Ordinal) + 6)..]);
        body.ShouldContain("Version: 1.4.2.0");
        body.ShouldContain("Host: Web");
        body.ShouldContain("Engine: PostgreSQL");
    }

    [Fact]
    public void Link_WithoutAConnection_SaysSo()
    {
        var link = BugReportLink.Create("1.4.2.0", "Desktop", null);

        Uri.UnescapeDataString(link).ShouldContain("Engine: no connection");
    }

    [Fact]
    public async Task Open_ReportsTheActiveTabsEngine()
    {
        var connection = new ConnectionModel { Id = Guid.NewGuid(), Name = "local", Type = DatabaseType.MySQL };
        _connections.Connections.Add(connection);
        var tab = _queries.AddQuery("Query1");
        tab.ConnectionId = connection.Id;
        _queries.SetActive(tab);

        await new BugReporter(_links, _connections, _queries).OpenAsync();

        await _links.Received(1).OpenLinkAsync(Arg.Is<string>(url => Uri.UnescapeDataString(url).Contains("Engine: MySQL")));
    }
}
