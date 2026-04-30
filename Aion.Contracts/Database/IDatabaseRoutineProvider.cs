namespace Aion.Contracts.Database;

public interface IDatabaseRoutineProvider
{
    Task<List<RoutineInfo>> GetRoutinesAsync(string connectionString, string database);
}
