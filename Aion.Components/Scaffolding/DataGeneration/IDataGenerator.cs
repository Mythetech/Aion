namespace Aion.Components.Scaffolding.DataGeneration;

public interface IDataGenerator
{
    string Name { get; }
    string Description { get; }

    /// <summary>Whether every value this generator makes can be stored in a column of this type.</summary>
    bool Supports(ColumnTypeShape type);

    object? Generate(int rowIndex, DataGeneratorOptions options);
}

public class DataGeneratorOptions
{
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public string? CustomValues { get; set; }
    public int? StartValue { get; set; }

    /// <summary>
    /// The column's highest value when generation starts, read by the service so numbering without a chosen
    /// start continues after the rows already there instead of colliding with them.
    /// </summary>
    public long? ExistingMaximum { get; set; }

    /// <summary>Values the referenced column holds, read by the service before a foreign key is filled.</summary>
    public IReadOnlyList<object>? ReferencedValues { get; set; }
}
