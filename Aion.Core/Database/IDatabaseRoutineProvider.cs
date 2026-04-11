namespace Aion.Core.Database;

public interface IDatabaseRoutineProvider
{
    Task<List<RoutineInfo>> GetRoutinesAsync(string connectionString, string database);
}
