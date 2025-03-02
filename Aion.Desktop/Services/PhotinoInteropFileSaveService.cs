using Aion.Components.Infrastructure;

namespace Aion.Desktop.Services;

public class PhotinoInteropFileSaveService : IFileSaveService
{
    private readonly IPhotinoAppProvider _provider;

    public PhotinoInteropFileSaveService(IPhotinoAppProvider provider)
    {
        _provider = provider;
    }
    
    public async Task<bool> SaveFileAsync(string fileName, string data)
    {
        string? location = await PromptFileSaveAsync(fileName);

        if (string.IsNullOrWhiteSpace(location))
            return false;
        
        await File.WriteAllTextAsync(location, data);
        return true;
    }

    public async Task<string?> PromptFileSaveAsync(string fileName)
    {
        var app = _provider.Instance;

        string? location = await app.MainWindow.ShowSaveFileAsync("Save File", null, [(fileName, ["sql"])]);
        
        return string.IsNullOrEmpty(location) ? default : location;
    }
}