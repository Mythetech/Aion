namespace Aion.Components.Infrastructure;

public interface IFileSaveService
{
    public Task<bool> SaveFileAsync(string fileName, string data);
}