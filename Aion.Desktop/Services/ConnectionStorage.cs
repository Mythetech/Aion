using Aion.Components.Connections;
using Aion.Contracts.Connections;

namespace Aion.Desktop.Services;

public interface IConnectionStorage
{
    Task SaveConnectionsAsync(IEnumerable<ConnectionModel> connections);
    Task<IEnumerable<ConnectionModel>> LoadConnectionsAsync();
}

/// <summary>
/// Saves every connection to a JSON file. Passwords never reach it: <see cref="ConnectionsFile"/> strips them as it
/// writes, and they live in the secret manager each connection names instead.
/// </summary>
public class FileConnectionStorage : IConnectionStorage
{
    private readonly string _storageFile;

    public FileConnectionStorage(string storageFile)
    {
        _storageFile = storageFile;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aion", "connections.json");

    public async Task SaveConnectionsAsync(IEnumerable<ConnectionModel> connections)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storageFile)!);

        // Write then move so a crash mid-write leaves the previous connections intact rather than a truncated file.
        var tempPath = _storageFile + ".tmp";
        await File.WriteAllTextAsync(tempPath, ConnectionsFile.Serialize(connections));
        File.Move(tempPath, _storageFile, overwrite: true);
    }

    public async Task<IEnumerable<ConnectionModel>> LoadConnectionsAsync()
    {
        if (!File.Exists(_storageFile))
            return [];

        var contents = ConnectionsFile.Deserialize(await File.ReadAllTextAsync(_storageFile));

        // Files written before passwords moved to secret managers hold them in plain text. They are not moved
        // anywhere: the file is written again straight away so they are off disk after the first launch.
        if (contents.WipedPasswords)
            await SaveConnectionsAsync(contents.Connections);

        return contents.Connections;
    }
}
