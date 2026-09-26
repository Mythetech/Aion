using Aion.Components.RequestContextPanel;
using Shouldly;

namespace Aion.Test.Unit;

public class SidePanelLayoutTests
{
    [Theory]
    [InlineData(1440, 1008)]
    [InlineData(1080, 720)]
    [InlineData(600, 300)]
    public void OpenDividerPosition_LeavesTheEditorAtLeastHalfAndThePanelReadable(int containerWidth, int divider)
    {
        SidePanelLayout.OpenDividerPosition(containerWidth).ShouldBe(divider);
    }

    [Fact]
    public void ClosedDividerPosition_LeavesTheCollapsedStrip()
    {
        SidePanelLayout.ClosedDividerPosition(1080).ShouldBe(1028);
    }
}
