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
        var app = _provider.Instance;
        var location = await app.MainWindow.ShowSaveFileAsync("Save File", null, [(fileName, ["sql"])]);
        if (string.IsNullOrEmpty(location))
            return false;
        await File.WriteAllTextAsync(location, data);
        return true;
    }
}