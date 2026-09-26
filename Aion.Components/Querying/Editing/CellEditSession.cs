namespace Aion.Components.Querying.Editing;

/// <summary>
/// Where a committed or navigated cell edit moves the grid's focus next.
/// </summary>
public enum CellEditMove
{
    None,
    Up,
    Down,
    Left,
    Right,
    Next,
    Previous
}

/// <summary>
/// One grid cell being edited: which cell, and the text typed so far, where null means NULL. The grid owns it
/// rather than the editor, so what was typed survives the editor being re-created, for example when its row
/// scrolls out of view and back.
/// </summary>
public sealed class CellEditSession
{
    public CellEditSession(int rowIndex, string column, string? text, EditableColumn rules, bool selectOnOpen = true)
    {
        RowIndex = rowIndex;
        Column = column;
        Text = text;
        Rules = rules;
        SelectOnOpen = selectOnOpen;
    }

    public int RowIndex { get; }

    public string Column { get; }

    public EditableColumn Rules { get; }

    /// <summary>
    /// Whether the editor selects its text when it opens, so typing replaces it; an editor opened by typing a
    /// character keeps the caret after it instead.
    /// </summary>
    public bool SelectOnOpen { get; }

    public string? Text { get; private set; }

    /// <summary>
    /// The text to store, null meaning NULL. Empty text stores NULL, as edit mode always has.
    /// </summary>
    public string? CommitText => string.IsNullOrEmpty(Text) ? null : Text;

    public void Type(string text) => Text = text;
}
