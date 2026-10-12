using Aion.Components.Connections;
using Aion.Contracts.Connections;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionPanelIdTests
{
    private static readonly ConnectionModel Orders = new() { Name = "Orders" };
    private static readonly ConnectionModel Billing = new() { Name = "Billing" };

    [Fact]
    public void IsOrphaned_PanelOfAConnectionThatIsGone_IsOrphaned()
    {
        ConnectionPanelId.IsOrphaned(Orders.Id.ToString(), [Billing]).ShouldBeTrue();
    }

    [Fact]
    public void IsOrphaned_PanelOfAConnectionStillListed_IsNot()
    {
        ConnectionPanelId.IsOrphaned(Orders.Id.ToString(), [Billing, Orders]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("history")]
    [InlineData("plugin-Sample.Plugin.SamplePanel")]
    public void IsOrphaned_PanelThatIsNotAConnections_IsNot(string panelId)
    {
        ConnectionPanelId.IsOrphaned(panelId, [Billing]).ShouldBeFalse();
    }

    [Fact]
    public void IsOrphaned_NoPanelOpen_IsNot()
    {
        ConnectionPanelId.IsOrphaned(null, [Billing]).ShouldBeFalse();
    }
}
