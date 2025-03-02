namespace Aion.Components.Infrastructure;

public interface IFileSaveService
{
    public Task<bool> SaveFileAsync(string fileName, string data);
    
    public Task<string?> PromptFileSaveAsync(string fileName);
}