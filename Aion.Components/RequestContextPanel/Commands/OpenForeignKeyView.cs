namespace Aion.Components.RequestContextPanel.Commands;

/// <param name="Choices">
/// Every foreign key of the same row with its own value, offered as alternatives in the viewer.
/// </param>
public record OpenForeignKeyView(ForeignKeyDetail ForeignKeyDetail, IReadOnlyList<ForeignKeyDetail>? Choices = null);
