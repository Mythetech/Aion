using Aion.Components.Metrics.Events;
using Aion.Components.Metrics.Services;
using Aion.Components.Metrics.Settings;
using Aion.Contracts.Connections;
using Aion.Contracts.Metrics;
using Shouldly;

namespace Aion.Test.Unit.Metrics;

public class MetricsStateTests
{
    private readonly MetricsState _state;
    private readonly MetricsSettings _settings;

    public MetricsStateTests()
    {
        _settings = new MetricsSettings { MaxDataPoints = 5 };
        _state = new MetricsState(_settings);
    }

    [Fact]
    public void RecordQueryMetric_StoresInConnectionData()
    {
        var connectionId = Guid.NewGuid();
        var metric = new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(100), 10, true);

        _state.RecordQueryMetric(metric);

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.QueryCount.ShouldBe(1);
        data.AverageQueryDuration.TotalMilliseconds.ShouldBe(100);
        data.ErrorRate.ShouldBe(0);
    }

    [Fact]
    public void RecordQueryMetric_MultipleQueries_ComputesCorrectAverages()
    {
        var connectionId = Guid.NewGuid();
        _state.RecordQueryMetric(new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(100), 10, true));
        _state.RecordQueryMetric(new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(200), 5, true));
        _state.RecordQueryMetric(new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(300), 0, false));

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.QueryCount.ShouldBe(3);
        data.AverageQueryDuration.TotalMilliseconds.ShouldBe(200);
        data.ErrorRate.ShouldBe(1.0 / 3.0, 0.01);
    }

    [Fact]
    public void RecordQueryMetric_RespectsMaxDataPoints()
    {
        var connectionId = Guid.NewGuid();
        for (var i = 0; i < 10; i++)
        {
            _state.RecordQueryMetric(new ClientQueryMetricRecorded(
                connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(i * 10), 1, true));
        }

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.QueriesOverTime.Count.ShouldBe(5);
    }

    [Fact]
    public void RecordQueryMetric_FiresStateChanged()
    {
        var connectionId = Guid.NewGuid();
        var fired = false;
        _state.StateChanged += () => fired = true;

        _state.RecordQueryMetric(new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(50), 1, true));

        fired.ShouldBeTrue();
    }

    [Fact]
    public void RecordServerMetrics_StoresLatestSnapshot()
    {
        var connectionId = Guid.NewGuid();
        var pool = new ConnectionPoolSnapshot(5, 100);
        var health = new ServerHealthSnapshot(TimeSpan.FromHours(2), "PostgreSQL 16.1");
        var metrics = new ServerMetricsCollected(
            connectionId, DateTime.UtcNow, pool, health, null, null);

        _state.RecordServerMetrics(metrics);

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.LatestConnectionPool.ShouldBe(pool);
        data.LatestServerHealth.ShouldBe(health);
    }

    [Fact]
    public void RecordHealthChange_AppendsToHistory()
    {
        var connectionId = Guid.NewGuid();

        _state.RecordHealthChange(connectionId, ConnectionHealthStatus.Healthy);
        _state.RecordHealthChange(connectionId, ConnectionHealthStatus.Unhealthy);
        _state.RecordHealthChange(connectionId, ConnectionHealthStatus.Healthy);

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.HealthHistory.Count.ShouldBe(3);
    }

    [Fact]
    public void GetConnectionData_UnknownId_ReturnsNull()
    {
        _state.GetConnectionData(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void InitializeConnection_CreatesEmptyData()
    {
        var connectionId = Guid.NewGuid();

        _state.InitializeConnection(connectionId);

        var data = _state.GetConnectionData(connectionId);
        data.ShouldNotBeNull();
        data.QueryCount.ShouldBe(0);
    }

    [Fact]
    public void ClearConnection_RemovesData()
    {
        var connectionId = Guid.NewGuid();
        _state.InitializeConnection(connectionId);
        _state.RecordQueryMetric(new ClientQueryMetricRecorded(
            connectionId, DateTime.UtcNow, TimeSpan.FromMilliseconds(50), 1, true));

        _state.ClearConnection(connectionId);

        _state.GetConnectionData(connectionId).ShouldBeNull();
    }
}
