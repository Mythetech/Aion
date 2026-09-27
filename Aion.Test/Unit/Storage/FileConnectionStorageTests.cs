using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Desktop.Services;
using Shouldly;

namespace Aion.Test.Unit.Storage;

public class FileConnectionStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aion-connection-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "Aion", "connections.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Save_KeepsEveryConnectionAndNoPassword()
    {
        var stored = new ConnectionModel
        {
            Name = "Stored", Type = DatabaseType.PostgreSQL,
            ConnectionString = "Host=db;Username=app;Password=hunter2", PasswordStore = "macOS Keychain"
        };
        var asked = new ConnectionModel
        {
            Name = "Asked", Type = DatabaseType.MySQL, ConnectionString = "Server=db;Uid=app;Pwd=swordfish"
        };
        var storage = new FileConnectionStorage(FilePath);

        await storage.SaveConnectionsAsync([stored, asked]);

        var text = await File.ReadAllTextAsync(FilePath);
        text.ShouldNotContain("hunter2");
        text.ShouldNotContain("swordfish");
        var loaded = (await new FileConnectionStorage(FilePath).LoadConnectionsAsync()).ToList();
        loaded.Select(c => c.Name).ShouldBe(["Stored", "Asked"]);
        loaded.ShouldAllBe(c => c.UsesPassword && c.IsSavedConnection);
        loaded[0].PasswordStore.ShouldBe("macOS Keychain");
        loaded[1].PasswordStore.ShouldBeNull();
    }

    [Fact]
    public async Task Load_FileWithAPlaintextPassword_RewritesItWithoutThePassword()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, """
            [{"Id":"6f1d2c3b-0000-0000-0000-000000000001","Name":"Old","ConnectionString":"Host=db;Username=app;Password=hunter2",
              "Type":0,"SaveCredentials":true,"IsSavedConnection":true}]
            """);

        var loaded = (await new FileConnectionStorage(FilePath).LoadConnectionsAsync()).Single();

        loaded.UsesPassword.ShouldBeTrue();
        loaded.PasswordStore.ShouldBeNull();
        loaded.ConnectionString.ShouldNotContain("hunter2");
        (await File.ReadAllTextAsync(FilePath)).ShouldNotContain("hunter2");
    }

    [Fact]
    public async Task Load_FileWithoutPasswords_LeavesItAlone()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        const string json = """[{"Id":"6f1d2c3b-0000-0000-0000-000000000001","Name":"Trust","ConnectionString":"Host=db","Type":0}]""";
        await File.WriteAllTextAsync(FilePath, json);

        await new FileConnectionStorage(FilePath).LoadConnectionsAsync();

        (await File.ReadAllTextAsync(FilePath)).ShouldBe(json);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsNothing()
    {
        (await new FileConnectionStorage(FilePath).LoadConnectionsAsync()).ShouldBeEmpty();
    }
}
