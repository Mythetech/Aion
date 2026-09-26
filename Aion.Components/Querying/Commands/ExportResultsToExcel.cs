using Aion.Contracts.Queries;

namespace Aion.Components.Querying.Commands;

/// <param name="Result">The rows to export, or the active result when null.</param>
/// <param name="TotalRows">How many rows were fetched, when <paramref name="Result"/> holds only the rows find in results kept.</param>
public record ExportResultsToExcel(QueryResult? Result = null, int? TotalRows = null);
