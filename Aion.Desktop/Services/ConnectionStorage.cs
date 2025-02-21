using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aion.Core.Connections;

public interface IConnectionStorage
{
    Task SaveConnectionsAsync(IEnumerable<ConnectionModel> connections);
    Task<IEnumerable<ConnectionModel>> LoadConnectionsAsync();
}

public class FileConnectionStorage : IConnectionStorage
{
    private readonly string _storageFile;

    public FileConnectionStorage()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _storageFile = Path.Combine(appData, "Aion", "connections.json");
    }

    public async Task SaveConnectionsAsync(IEnumerable<ConnectionModel> connections)
    {
        var directory = Path.GetDirectoryName(_storageFile);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        var savedConnections = connections
            .Where(c => c.SaveCredentials)
            .ToList();
            
        await File.WriteAllTextAsync(_storageFile, 
            JsonSerializer.Serialize(savedConnections));
    }

    public async Task<IEnumerable<ConnectionModel>> LoadConnectionsAsync()
    {
        if (!File.Exists(_storageFile))
            return Enumerable.Empty<ConnectionModel>();

        var json = await File.ReadAllTextAsync(_storageFile);
        var connections = JsonSerializer.Deserialize<List<ConnectionModel>>(json);
        
        // Mark these as saved connections
        foreach (var conn in connections!)
            conn.IsSavedConnection = true;
            
        return connections;
    }
} 