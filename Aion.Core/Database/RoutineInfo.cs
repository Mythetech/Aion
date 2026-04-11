namespace Aion.Core.Database;

public enum RoutineKind
{
    Function,
    Procedure
}

public record RoutineInfo(
    string Schema,
    string Name,
    RoutineKind Kind,
    string? ReturnType,
    string? ArgumentSignature,
    string? Language)
{
    public string DisplayName =>
        string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
}
