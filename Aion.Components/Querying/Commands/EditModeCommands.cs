using Aion.Components.Querying.Editing;
using Aion.Core.Queries.Editing;

namespace Aion.Components.Querying.Commands;

/// <summary>
/// Enter edit mode for the current query result.
/// </summary>
public record EnterEditMode(QueryModel Query);

/// <summary>
/// Exit edit mode.
/// </summary>
public record ExitEditMode(bool DiscardChanges = true);

/// <summary>
/// Insert a new row.
/// </summary>
public record InsertRow();

/// <summary>
/// Duplicate an existing row.
/// </summary>
public record DuplicateRow(int SourceRowIndex);

/// <summary>
/// Delete selected rows.
/// </summary>
public record DeleteSelectedRows(List<int> RowIndices);

/// <summary>
/// Update a cell value.
/// </summary>
public record UpdateCell(int RowIndex, string Column, object? NewValue);

/// <summary>
/// Apply all pending changes to the database.
/// </summary>
public record ApplyPendingChanges(EditState EditState, EditableQueryResult EditableResult);

/// <summary>
/// Discard all pending changes.
/// </summary>
public record DiscardPendingChanges();

/// <summary>
/// Open a table in edit mode from the connection panel.
/// </summary>
public record OpenTableEditor(Guid ConnectionId, string DatabaseName, string TableName);

/// <summary>
/// Enable edit mode on the active query by parsing the SQL to determine the table.
/// </summary>
public record EnableEditModeFromQuery();
