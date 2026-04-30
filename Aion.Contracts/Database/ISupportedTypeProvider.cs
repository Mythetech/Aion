namespace Aion.Contracts.Database;

public interface ISupportedTypeProvider
{
    DatabaseType DatabaseType { get; }
    IReadOnlyList<string> SupportedTypes { get; }
}
