using Aion.Components.Querying;
using Shouldly;

namespace Aion.Test.Unit.Querying;

public class RowSelectionStateTests
{
    // Rows as a sorted grid shows them: result rows 4, 0, 3, 1, 2 from top to bottom.
    private static readonly int[] SortedOrder = [4, 0, 3, 1, 2];

    [Fact]
    public void CellClick_SelectsOnlyThatRow()
    {
        var selection = new RowSelectionState();
        selection.ToggleSelection(4, ctrlKey: false, shiftKey: false, SortedOrder);

        selection.ToggleSelection(3, ctrlKey: false, shiftKey: false, SortedOrder);

        selection.SelectedIndices.ShouldBe([3]);
    }

    [Fact]
    public void CtrlCellClick_TogglesThatRowAndKeepsTheRest()
    {
        var selection = new RowSelectionState();
        selection.ToggleSelection(4, ctrlKey: false, shiftKey: false, SortedOrder);

        selection.ToggleSelection(3, ctrlKey: true, shiftKey: false, SortedOrder);
        selection.ToggleSelection(4, ctrlKey: true, shiftKey: false, SortedOrder);

        selection.SelectedIndices.ShouldBe([3]);
    }

    [Fact]
    public void ShiftCellClick_SelectsTheRowsBetweenInTheOrderShown()
    {
        var selection = new RowSelectionState();
        selection.ToggleSelection(0, ctrlKey: false, shiftKey: false, SortedOrder);

        selection.ToggleSelection(1, ctrlKey: false, shiftKey: true, SortedOrder);

        selection.SelectedIndices.OrderBy(i => i).ShouldBe([0, 1, 3]);
    }

    [Fact]
    public void ShiftCellClick_WhenTheLastClickedRowIsNoLongerShown_SelectsOnlyThatRow()
    {
        var selection = new RowSelectionState();
        selection.ToggleSelection(4, ctrlKey: false, shiftKey: false, SortedOrder);

        selection.ToggleSelection(1, ctrlKey: false, shiftKey: true, [0, 3, 1]);

        selection.SelectedIndices.ShouldBe([1]);
    }

    [Fact]
    public void RowNumberClick_TogglesThatRowAndKeepsTheRest()
    {
        var selection = new RowSelectionState();
        selection.ToggleRow(4, shiftKey: false, SortedOrder);
        selection.ToggleRow(3, shiftKey: false, SortedOrder);

        selection.ToggleRow(4, shiftKey: false, SortedOrder);

        selection.SelectedIndices.ShouldBe([3]);
    }

    [Fact]
    public void ShiftRowNumberClick_AddsTheRowsBetweenInTheOrderShown()
    {
        var selection = new RowSelectionState();
        selection.ToggleRow(2, shiftKey: false, SortedOrder);
        selection.ToggleRow(0, shiftKey: false, SortedOrder);

        selection.ToggleRow(1, shiftKey: true, SortedOrder);

        selection.SelectedIndices.OrderBy(i => i).ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void ShiftRowNumberClick_OnASelectedRow_ClearsTheRowsBetweenInTheOrderShown()
    {
        var selection = new RowSelectionState();
        selection.SelectAll(SortedOrder);
        selection.ToggleRow(0, shiftKey: false, SortedOrder);
        selection.ToggleRow(0, shiftKey: false, SortedOrder);

        selection.ToggleRow(1, shiftKey: true, SortedOrder);

        selection.SelectedIndices.OrderBy(i => i).ShouldBe([2, 4]);
    }

    [Fact]
    public void CoverageOf_SaysWhetherNoneSomeOrAllOfTheGivenRowsAreSelected()
    {
        var selection = new RowSelectionState();
        selection.CoverageOf([0, 1, 2]).ShouldBe(SelectionCoverage.None);

        selection.SelectAll([1, 7]);
        selection.CoverageOf([0, 1, 2]).ShouldBe(SelectionCoverage.Some);
        selection.CoverageOf([0, 2]).ShouldBe(SelectionCoverage.None);

        selection.SelectAll([0, 1, 2, 7]);
        selection.CoverageOf([0, 1, 2]).ShouldBe(SelectionCoverage.All);
        selection.CoverageOf([]).ShouldBe(SelectionCoverage.None);
    }

    [Fact]
    public void SelectAll_SelectsExactlyTheGivenRows()
    {
        var selection = new RowSelectionState();
        selection.ToggleRow(4, shiftKey: false, SortedOrder);

        selection.SelectAll([0, 3]);

        selection.SelectedIndices.OrderBy(i => i).ShouldBe([0, 3]);
    }

    [Fact]
    public void EveryChange_RaisesSelectionChanged()
    {
        var selection = new RowSelectionState();
        var raised = 0;
        selection.SelectionChanged += () => raised++;

        selection.ToggleSelection(0, ctrlKey: false, shiftKey: false, SortedOrder);
        selection.ToggleRow(1, shiftKey: false, SortedOrder);
        selection.SelectAll([0]);
        selection.ClearSelection();

        raised.ShouldBe(4);
    }
}
