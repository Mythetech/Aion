using Aion.Components.RequestContextPanel;

namespace Aion.Components.ForeignKeys.Commands;

/// <summary>
/// Opens the rows a foreign key value points at in a new query tab and runs it.
/// </summary>
public record OpenForeignKeyRows(ForeignKeyDetail Detail);
