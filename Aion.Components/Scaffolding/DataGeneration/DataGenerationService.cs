using Aion.Contracts.Database;
using Aion.Contracts.Queries;

namespace Aion.Components.Scaffolding.DataGeneration;

public class DataGenerationService
{
    private const int BatchSize = 200;

    public async Task<int> GenerateAsync(
        DataGenerationModel model,
        IDatabaseProvider provider,
        string connectionString,
        string database,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalInserted = 0;

        for (int offset = 0; offset < model.RowCount; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchSize = Math.Min(BatchSize, model.RowCount - offset);
            var sql = BuildBatchInsert(model, offset, batchSize);

            await provider.ExecuteQueryAsync(connectionString, sql, cancellationToken);
            totalInserted += batchSize;
            progress?.Report(totalInserted);
        }

        return totalInserted;
    }

    private static string BuildBatchInsert(DataGenerationModel model, int startRow, int count)
    {
        var activeColumns = model.ColumnGenerators
            .Where(cg => cg.Generator is not null && !cg.Column.IsIdentity)
            .ToList();

        if (activeColumns.Count == 0)
            return string.Empty;

        var columnNames = string.Join(", ", activeColumns.Select(c => $"\"{c.Column.Name}\""));
        var rows = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            var values = activeColumns.Select(cg =>
            {
                var value = cg.Generator!.Generate(startRow + i, cg.Options);
                return FormatSqlValue(value);
            });
            rows.Add($"({string.Join(", ", values)})");
        }

        var schemaPrefix = string.IsNullOrEmpty(model.Schema) || model.Schema == "public"
            ? "" : $"\"{model.Schema}\".";

        return $"INSERT INTO {schemaPrefix}\"{model.TableName}\" ({columnNames})\nVALUES\n{string.Join(",\n", rows)};";
    }

    private static string FormatSqlValue(object? value)
    {
        if (value is null)
            return "NULL";

        if (value is bool b)
            return b ? "true" : "false";

        if (value is int or long or short or float or double or decimal)
            return value.ToString()!;

        var str = value.ToString()!;
        return $"'{str.Replace("'", "''")}'";
    }
}

public class DataGenerationModel
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public int RowCount { get; set; } = 100;
    public List<ColumnGeneratorBinding> ColumnGenerators { get; set; } = [];
}

public class ColumnGeneratorBinding
{
    public ColumnInfo Column { get; set; } = default!;
    public IDataGenerator? Generator { get; set; }
    public DataGeneratorOptions Options { get; set; } = new();
}
