namespace Aion.Components.Querying;

public interface IQuerySaveService
{
    Task SaveQueryAsync(QueryModel query);
    Task DeleteQueryAsync(QueryModel query);
    Task<IEnumerable<QueryModel>> LoadQueriesAsync();
}