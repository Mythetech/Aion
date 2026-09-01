using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Services;
using Aion.Components.Metrics.Settings;
using Aion.Contracts.Database;
using Aion.Contracts.Metrics;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Metrics;

public class MetricsPollingServiceTests
{
    private readonly IMessageBus _bus;
    private readonly MetricsSettings _settings;
    private readonly ILogger<MetricsPollingService> _logger;
    private const string TestConnectionString = "Host=localhost;Database=testdb";

    public MetricsPollingServiceTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _settings = new MetricsSettings { PollingIntervalSeconds = 5 };
        _logger = Substitute.For<ILogger<MetricsPollingService>>();
    }

    [Fact]
    public async Task PollConnectionAsync_WithConnectionMetrics_PublishesSnapshot()
    {
        var connectionId = Guid.NewGuid();
        var provider = (IDatabaseProvider)Substitute.For(new[] { typeof(IDatabaseProvider), typeof(IDatabaseConnectionMetrics) }, Array.Empty<object>());
        var connMetrics = (IDatabaseConnectionMetrics)provider;
        connMetrics.GetActiveConnectionCountAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(5);
        connMetrics.GetMaxConnectionsAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(100);

        var service = new MetricsPollingService(_bus, _settings, _logger);
        await service.PollConnectionAsync(connectionId, provider, TestConnectionString, "testdb", CancellationToken.None);

        await _bus.Received(1).PublishAsync(
            Arg.Is<ServerMetricsCollected>(m =>
                m.ConnectionId == connectionId &&
                m.ConnectionPool != null &&
                m.ConnectionPool.ActiveConnections == 5 &&
                m.ConnectionPool.MaxConnections == 100));
    }

    [Fact]
    public async Task PollConnectionAsync_WithoutCapabilities_PublishesAllNulls()
    {
        var connectionId = Guid.NewGuid();
        var provider = Substitute.For<IDatabaseProvider>();

        var service = new MetricsPollingService(_bus, _settings, _logger);
        await service.PollConnectionAsync(connectionId, provider, TestConnectionString, "testdb", CancellationToken.None);

        await _bus.Received(1).PublishAsync(
            Arg.Is<ServerMetricsCollected>(m =>
                m.ConnectionPool == null &&
                m.ServerHealth == null &&
                m.Storage == null &&
                m.Performance == null));
    }

    [Fact]
    public async Task PollConnectionAsync_WithAllCapabilities_PopulatesAllSections()
    {
        var connectionId = Guid.NewGuid();
        var provider = (IDatabaseProvider)Substitute.For(
            new[]
            {
                typeof(IDatabaseProvider),
                typeof(IDatabaseConnectionMetrics),
                typeof(IDatabaseServerHealthMetrics),
                typeof(IDatabaseStorageMetrics),
                typeof(IDatabasePerformanceMetrics)
            },
            Array.Empty<object>());

        var connMetrics = (IDatabaseConnectionMetrics)provider;
        connMetrics.GetActiveConnectionCountAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(3);
        connMetrics.GetMaxConnectionsAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(50);

        var healthMetrics = (IDatabaseServerHealthMetrics)provider;
        healthMetrics.GetServerUptimeAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(TimeSpan.FromHours(1));
        healthMetrics.GetServerVersionAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns("PostgreSQL 16.1");

        var storageMetrics = (IDatabaseStorageMetrics)provider;
        storageMetrics.GetDatabaseSizeAsync(TestConnectionString, "testdb", Arg.Any<CancellationToken>())
            .Returns(new DatabaseSizeInfo("testdb", 1024000, 5));
        storageMetrics.GetTableSizesAsync(TestConnectionString, "testdb", Arg.Any<CancellationToken>())
            .Returns(new List<TableSizeInfo> { new("users", 512000, 1000) });

        var perfMetrics = (IDatabasePerformanceMetrics)provider;
        perfMetrics.GetCacheHitRatioAsync(TestConnectionString, Arg.Any<CancellationToken>()).Returns(0.95);
        perfMetrics.GetActiveQueriesAsync(TestConnectionString, Arg.Any<CancellationToken>())
            .Returns(new List<ActiveQueryInfo>());

        var service = new MetricsPollingService(_bus, _settings, _logger);
        await service.PollConnectionAsync(connectionId, provider, TestConnectionString, "testdb", CancellationToken.None);

        await _bus.Received(1).PublishAsync(
            Arg.Is<ServerMetricsCollected>(m =>
                m.ConnectionPool != null &&
                m.ServerHealth != null &&
                m.Storage != null &&
                m.Performance != null));
    }
}
