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
    /// Whether committing now would store NULL, which the editor shows in place of the empty text.
    /// </summary>
    public bool ShowsNull => Text is null || (Text.Length == 0 && !Rules.AcceptsEmptyText && Rules.IsNullable);

    public void Type(string text) => Text = text;

    /// <returns>False when the column can't hold NULL, which leaves the text as it was.</returns>
    public bool SetNull()
    {
        if (!Rules.IsNullable)
        {
            return false;
        }

        Text = null;
        return true;
    }

    /// <summary>
    /// The text to store, null meaning NULL. Empty text in a column whose type can't hold it means NULL when the
    /// column allows it; otherwise there is nothing valid to commit.
    /// </summary>
    public bool TryGetCommitText(out string? text)
    {
        if (Text is { Length: 0 } && !Rules.AcceptsEmptyText)
        {
            text = null;
            return Rules.IsNullable;
        }

        text = Text;
        return true;
    }
}
