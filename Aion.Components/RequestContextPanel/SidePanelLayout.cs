namespace Aion.Components.RequestContextPanel;

public static class SidePanelLayout
{
    public const int CollapsedWidth = 52;

    // The foreign key and JSON views lay out a label, a value and actions side by side, which clip below this.
    private const int MinOpenWidth = 360;

    /// <summary>
    /// Where the divider goes when the side panel opens: about 30% of the width, widened to stay readable,
    /// but never more than half so the editor keeps the larger share.
    /// </summary>
    public static int OpenDividerPosition(int containerWidth)
    {
        var half = containerWidth / 2;
        var panelWidth = Math.Clamp((int)(containerWidth * 0.3), Math.Min(MinOpenWidth, half), half);
        return containerWidth - panelWidth;
    }

    public static int ClosedDividerPosition(int containerWidth) => containerWidth - CollapsedWidth;
}
