namespace Aion.Components.Scaffolding.DataGeneration;

public interface IDataGenerator
{
    string Name { get; }
    string Description { get; }
    bool SupportsType(string dataType);
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
}
