using System.Globalization;
using System.Text;
using Aion.Contracts.Database;
using Aion.Contracts.Database.Dialects;
using Aion.Contracts.Queries;

namespace Aion.Components.Scaffolding.DataGeneration;

/// <summary>
/// Inserts generated rows into a table. Every row goes in one transaction, so a rejected batch leaves the
/// table as it was rather than holding the batches before it, and every value is written through the
/// engine's dialect so quoting, escaping and booleans follow that engine's rules.
/// </summary>
public class DataGenerationService
{
    private const int MaxRowsPerStatement = 200;

    // Keeps a statement of wide rows well inside SQLite's default 1,000,000 byte statement limit.
    private const int MaxStatementLength = 250_000;

    private const int ReferencedValueSample = 1000;

    /// <param name="progress">Called with the number of rows inserted so far after each statement.</param>
    public async Task<DataGenerationResult> GenerateAsync(
        DataGenerationModel model,
        IDatabaseProvider provider,
        string connectionString,
        Func<int, Task>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (provider is not ISqlDialectProvider { Dialect: var dialect })
            return DataGenerationResult.Failed($"Generating data is not supported for {provider.DatabaseType} connections.");

        var problems = DataGenerationPlan.Problems(model);
        if (problems.Count > 0)
            return DataGenerationResult.Failed(problems[0]);

        cancellationToken.ThrowIfCancellationRequested();

        TransactionInfo transaction;
        try
        {
            transaction = await provider.BeginTransactionAsync(connectionString);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DataGenerationResult.Failed($"Could not start a transaction: {ex.Message}");
        }

        var run = new Run(provider, connectionString, transaction.Id, dialect, model);
        try
        {
            var error = await run.InsertAsync(progress, cancellationToken);
            if (error is not null)
            {
                await run.RollBackQuietlyAsync();
                return DataGenerationResult.Failed(error);
            }

            await provider.CommitTransactionAsync(connectionString, transaction.Id);
            return DataGenerationResult.Inserted(model.RowCount);
        }
        catch (OperationCanceledException)
        {
            await run.RollBackQuietlyAsync();
            throw;
        }
        catch (Exception ex)
        {
            await run.RollBackQuietlyAsync();
            return DataGenerationResult.Failed($"Generating data failed, so no rows were added: {ex.Message}");
        }
    }

    // Some providers only report the driver's text, which carries prefixes such as "Worker error: SQLITE_ERROR:".
    private static string EngineMessage(QueryResult result) =>
        (result.ErrorDetail ?? QueryErrorNormalizer.Normalize(result.Error ?? "unknown error")).Message;

    private sealed class Run(IDatabaseProvider provider, string connectionString, string transactionId, SqlDialect dialect, DataGenerationModel model)
    {
        private readonly List<ColumnGeneratorBinding> _columns = model.ColumnGenerators.Where(b => b.WritesValue).ToList();
        private string Table => dialect.QualifyTable(model.Schema, model.TableName);

        public async Task<string?> InsertAsync(Func<int, Task>? progress, CancellationToken cancellationToken)
        {
            var error = await ReadExistingValuesAsync(cancellationToken);
            if (error is not null)
                return error;

            var inserted = 0;
            while (inserted < model.RowCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (sql, count) = BuildInsert(inserted);
                var result = await provider.ExecuteInTransactionAsync(connectionString, sql, transactionId, cancellationToken);
                if (!result.Success)
                    return $"Inserting rows {inserted + 1} to {inserted + count} failed, so no rows were added: {EngineMessage(result)}";

                inserted += count;
                if (progress is not null)
                    await progress(inserted);
            }

            return null;
        }

        /// <summary>
        /// Reads what generated values depend on: the values a foreign key may point at, and where numbering
        /// continues. Both are read inside the transaction, so they match what the inserts will see.
        /// </summary>
        private async Task<string?> ReadExistingValuesAsync(CancellationToken cancellationToken)
        {
            foreach (var binding in _columns)
            {
                switch (binding.Generator)
                {
                    case ReferencedValueGenerator when binding.Column.ForeignKey is { } foreignKey:
                        var referenced = await QueryAsync(ReferencedValuesSql(foreignKey), cancellationToken);
                        if (referenced.Error is not null)
                            return $"Could not read the values \"{binding.Column.Name}\" can refer to: {referenced.Error}";

                        binding.Options.ReferencedValues = referenced.Values(foreignKey.ReferencedColumn);
                        if (binding.Options.ReferencedValues.Count == 0 && !binding.Column.IsNullable)
                            return $"\"{binding.Column.Name}\" must match a row in {foreignKey.ReferencedTable}, which has none yet. " +
                                   $"Generate data for {foreignKey.ReferencedTable} first.";
                        break;

                    case AutoIncrementGenerator when binding.Options.StartValue is null:
                        var maximum = await QueryAsync(
                            $"SELECT MAX({dialect.QuoteIdentifier(binding.Column.Name)}) AS {dialect.QuoteIdentifier("max_value")} FROM {Table};",
                            cancellationToken);
                        if (maximum.Error is not null)
                            return $"Could not read the highest \"{binding.Column.Name}\" to number from: {maximum.Error}";

                        binding.Options.ExistingMaximum = maximum.Values().FirstOrDefault() is { } value ? WholeNumber(value) : null;
                        break;
                }
            }

            return null;
        }

        private string ReferencedValuesSql(ForeignKeyInfo foreignKey) => dialect.SelectRows(
            dialect.QualifyTable(foreignKey.ReferencedSchema, foreignKey.ReferencedTable),
            $"{dialect.QuoteIdentifier(foreignKey.ReferencedColumn)} IS NOT NULL",
            ReferencedValueSample);

        private async Task<Lookup> QueryAsync(string sql, CancellationToken cancellationToken)
        {
            var result = await provider.ExecuteInTransactionAsync(connectionString, sql, transactionId, cancellationToken);
            return new Lookup(result);
        }

        private (string Sql, int Rows) BuildInsert(int firstRow)
        {
            var sql = new StringBuilder()
                .Append("INSERT INTO ").Append(Table).Append('\n')
                .Append('(').Append(string.Join(", ", _columns.Select(b => dialect.QuoteIdentifier(b.Column.Name)))).Append(")\n")
                .Append("VALUES\n");

            var rows = 0;
            while (firstRow + rows < model.RowCount && rows < MaxRowsPerStatement && (rows == 0 || sql.Length < MaxStatementLength))
            {
                var rowIndex = firstRow + rows;
                var values = _columns.Select(b => dialect.FormatLiteral(GeneratedValues.Fit(b.Generator!.Generate(rowIndex, b.Options), b.Type)));

                if (rows > 0)
                    sql.Append(",\n");
                sql.Append('(').Append(string.Join(", ", values)).Append(')');
                rows++;
            }

            return (sql.Append(';').ToString(), rows);
        }

        public async Task RollBackQuietlyAsync()
        {
            // The failure that led here is what the user needs to see; a rollback that also fails would only hide it.
            try
            {
                await provider.RollbackTransactionAsync(connectionString, transactionId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }

        private static long? WholeNumber(object value)
        {
            try
            {
                return (long)decimal.Floor(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                return null;
            }
        }
    }

    private sealed class Lookup(QueryResult result)
    {
        public string? Error => result.Success ? null : EngineMessage(result);

        /// <summary>The distinct non-null values of one column, or of the first column when none is named.</summary>
        public IReadOnlyList<object> Values(string? column = null)
        {
            var ordinal = column is null ? 0 : result.Headers().FindIndex(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
            if (ordinal < 0 || ordinal >= result.Columns.Count)
                return [];

            var key = result.Columns[ordinal];
            return result.Rows
                .Select(row => row.GetValueOrDefault(key))
                .OfType<object>()
                .Where(value => value is not DBNull)
                .Distinct()
                .ToList();
        }
    }
}

public sealed record DataGenerationResult(int RowsInserted, string? Error)
{
    public bool Success => Error is null;

    public static DataGenerationResult Inserted(int rows) => new(rows, null);

    public static DataGenerationResult Failed(string error) => new(0, error);
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

    public ColumnTypeShape Type { get; set; } = new(string.Empty, ColumnTypeFamily.Unknown);

    /// <summary>
    /// Why the database writes this column itself ("identity", "rowid", "rowversion"), in which case it is left out
    /// of every INSERT; null when Aion generates its values.
    /// </summary>
    public string? FilledByDatabase { get; set; }

    public IDataGenerator? Generator { get; set; }
    public DataGeneratorOptions Options { get; set; } = new();

    /// <summary>Whether the column appears in the INSERT, as opposed to being left to the database.</summary>
    public bool WritesValue => FilledByDatabase is null && Generator is not null and not DatabaseDefaultGenerator;
}
