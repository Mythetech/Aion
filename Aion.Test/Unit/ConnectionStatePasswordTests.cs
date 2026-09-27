using Aion.Components.Connections;
using Aion.Components.Connections.Secrets;
using Aion.Components.Shared.Snackbar.Commands;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class ConnectionStatePasswordTests
{
    private const string WithPassword = "Host=db;Username=app;Password=hunter2";
    private const string WithoutPassword = "Host=db;Username=app";

    private readonly IConnectionService _connectionService = Substitute.For<IConnectionService>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly ConnectionSecretStoreFake _secrets = new();
    private readonly ConnectionState _sut;

    public ConnectionStatePasswordTests()
    {
        _sut = new ConnectionState(_connectionService, Substitute.For<IDatabaseProviderFactory>(), _messageBus,
            NullLogger<ConnectionState>.Instance, _secrets);
        ServerAccepts();
    }

    private void ServerAccepts() =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>()).Returns(["app"]);

    private void ServerRejects(string message) =>
        _connectionService.GetDatabasesAsync(Arg.Any<string>(), Arg.Any<DatabaseType>())
            .Returns<List<string>?>(_ => throw new InvalidOperationException(message));

    private static ConnectionModel NewConnection(string connectionString = WithPassword) => new()
    {
        Name = "Prod",
        Type = DatabaseType.PostgreSQL,
        ConnectionString = connectionString
    };

    private ConnectionModel SavedConnection(string? passwordStore, string connectionString = WithoutPassword)
    {
        var connection = NewConnection(connectionString);
        connection.UsesPassword = true;
        connection.PasswordStore = passwordStore;
        _connectionService.GetSavedConnections().Returns([connection]);
        return connection;
    }

    private ConnectionModel ExistingConnection(string? passwordStore)
    {
        var connection = NewConnection();
        connection.UsesPassword = true;
        connection.PasswordStore = passwordStore;
        connection.Active = true;
        _sut.Connections.Add(connection);
        return connection;
    }

    private Task ReceivedWarning(string message) =>
        _messageBus.Received(1).PublishAsync(Arg.Is<AddNotification>(n => n.Severity == Severity.Warning && n.Message == message));

    // Connect

    [Fact]
    public async Task Connect_Store_SavesThePasswordAndRecordsWhereItWent()
    {
        var connection = NewConnection();
        string? storeWhenSaved = null;
        await _connectionService.AddConnection(Arg.Do<ConnectionModel>(c => storeWhenSaved = c.PasswordStore));

        await _sut.ConnectAsync(connection, PasswordChoice.Store);

        _secrets.Passwords[(ConnectionSecretStoreFake.Vault, connection.Id)].ShouldBe("hunter2");
        connection.PasswordStore.ShouldBe(ConnectionSecretStoreFake.Vault);
        connection.UsesPassword.ShouldBeTrue();
        storeWhenSaved.ShouldBe(ConnectionSecretStoreFake.Vault);
        connection.ConnectionString.ShouldBe(WithPassword);
    }

    [Fact]
    public async Task Connect_DontStore_SavesTheConnectionButNotThePassword()
    {
        var connection = NewConnection();

        await _sut.ConnectAsync(connection, PasswordChoice.DontStore);

        _secrets.Passwords.ShouldBeEmpty();
        connection.PasswordStore.ShouldBeNull();
        connection.UsesPassword.ShouldBeTrue();
        await _connectionService.Received(1).AddConnection(connection);
    }

    [Fact]
    public async Task Connect_StoreFails_KeepsTheConnectionAndWarns()
    {
        _secrets.SaveError = "The keychain is locked.";
        var connection = NewConnection();

        var result = await _sut.ConnectAsync(connection, PasswordChoice.Store);

        result.Success.ShouldBeTrue();
        _sut.Connections.ShouldContain(connection);
        connection.PasswordStore.ShouldBeNull();
        await _connectionService.Received(1).AddConnection(connection);
        await ReceivedWarning("Aion couldn't store the password in Test Vault: The keychain is locked.");
    }

    [Fact]
    public async Task Connect_WithoutAPassword_StoresNothing()
    {
        var connection = NewConnection("Server=db;Integrated Security=True");
        connection.Type = DatabaseType.SQLServer;

        await _sut.ConnectAsync(connection, PasswordChoice.Store);

        connection.UsesPassword.ShouldBeFalse();
        connection.PasswordStore.ShouldBeNull();
        _secrets.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_ServerRejects_StoresNothing()
    {
        ServerRejects("password authentication failed");

        await _sut.ConnectAsync(NewConnection(), PasswordChoice.Store);

        _secrets.Calls.ShouldBeEmpty();
    }

    // Edit

    [Fact]
    public async Task Edit_Store_MovesThePasswordFromWhereItWas()
    {
        var connection = ExistingConnection("Old Vault");

        await _sut.UpdateConnection(connection.Id, NewConnection("Host=db;Username=app;Password=new-secret"), PasswordChoice.Store);

        _secrets.Calls.ShouldBe(["save Test Vault previous Old Vault"]);
        connection.PasswordStore.ShouldBe(ConnectionSecretStoreFake.Vault);
        await _connectionService.Received(1).UpdateConnection(Arg.Is<ConnectionModel>(c => c.PasswordStore == ConnectionSecretStoreFake.Vault));
    }

    [Fact]
    public async Task Edit_DontStore_DeletesTheStoredPassword()
    {
        var connection = ExistingConnection(ConnectionSecretStoreFake.Vault);
        _secrets.Passwords[(ConnectionSecretStoreFake.Vault, connection.Id)] = "hunter2";

        await _sut.UpdateConnection(connection.Id, NewConnection(), PasswordChoice.DontStore);

        _secrets.Passwords.ShouldBeEmpty();
        connection.PasswordStore.ShouldBeNull();
    }

    [Fact]
    public async Task Edit_UnchangedPasswordAlreadyInTheActiveStore_IsNotWrittenAgain()
    {
        var connection = ExistingConnection(ConnectionSecretStoreFake.Vault);

        await _sut.UpdateConnection(connection.Id, NewConnection(), PasswordChoice.Store);

        _secrets.Calls.ShouldBeEmpty();
        connection.PasswordStore.ShouldBe(ConnectionSecretStoreFake.Vault);
    }

    [Fact]
    public async Task Edit_StoreFailsWithAChangedPassword_RecordsNoStoreAndDropsTheStaleCopy()
    {
        var connection = ExistingConnection("Old Vault");
        _secrets.Passwords[("Old Vault", connection.Id)] = "hunter2";
        _secrets.SaveError = "The keychain is locked.";

        await _sut.UpdateConnection(connection.Id, NewConnection("Host=db;Username=app;Password=new-secret"), PasswordChoice.Store);

        connection.PasswordStore.ShouldBeNull();
        _secrets.Passwords.ShouldBeEmpty();
        await ReceivedWarning("Aion couldn't store the password in Test Vault: The keychain is locked.");
    }

    [Fact]
    public async Task Edit_StoreFailsWithTheSamePassword_LeavesItWhereItWas()
    {
        var connection = ExistingConnection("macOS Keychain");
        _secrets.Passwords[("macOS Keychain", connection.Id)] = "hunter2";
        _secrets.MakeReadOnly("1Password CLI");

        await _sut.UpdateConnection(connection.Id, NewConnection(), PasswordChoice.Store);

        connection.PasswordStore.ShouldBe("macOS Keychain");
        _secrets.Passwords[("macOS Keychain", connection.Id)].ShouldBe("hunter2");
        await ReceivedWarning("Aion couldn't store the password: 1Password CLI can't store passwords. It stays in macOS Keychain.");
    }

    // Remove and rename

    [Fact]
    public async Task Remove_DeletesThePasswordFromItsStore()
    {
        var connection = ExistingConnection(ConnectionSecretStoreFake.Vault);
        _secrets.Passwords[(ConnectionSecretStoreFake.Vault, connection.Id)] = "hunter2";

        await _sut.RemoveConnection(connection.Id);

        _secrets.Passwords.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_PasswordNotStored_TouchesNoStore()
    {
        var connection = ExistingConnection(passwordStore: null);

        await _sut.RemoveConnection(connection.Id);

        _secrets.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_TouchesNoStore()
    {
        var connection = ExistingConnection(ConnectionSecretStoreFake.Vault);

        await _sut.RenameConnectionAsync(connection.Id, "Reporting");

        _secrets.Calls.ShouldBeEmpty();
    }

    // Startup

    [Fact]
    public async Task Startup_StoredPassword_IsInjectedAndConnects()
    {
        var connection = SavedConnection(ConnectionSecretStoreFake.Vault);
        _secrets.Passwords[(ConnectionSecretStoreFake.Vault, connection.Id)] = "hunter2";

        await _sut.InitializeAsync();

        connection.Active.ShouldBeTrue();
        ConnectionPasswords.GetPassword(DatabaseType.PostgreSQL, connection.ConnectionString).ShouldBe("hunter2");
        await _connectionService.Received(1).GetDatabasesAsync(Arg.Is<string>(cs => cs.Contains("hunter2")), DatabaseType.PostgreSQL);
    }

    [Fact]
    public async Task Startup_PasswordNotStored_NeedsPasswordWithoutConnecting()
    {
        var connection = SavedConnection(passwordStore: null);

        await _sut.InitializeAsync();

        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
        connection.Active.ShouldBeFalse();
        connection.LastError.ShouldBe(ConnectionState.PasswordNotStoredReason);
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    [Fact]
    public async Task Startup_PasswordMissingFromItsStore_NeedsPasswordWithTheStoresReason()
    {
        var connection = SavedConnection(ConnectionSecretStoreFake.Vault);

        await _sut.InitializeAsync();

        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
        connection.LastError.ShouldBe("The password wasn't found in Test Vault");
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    [Fact]
    public async Task Startup_StoreUnavailable_NeedsPasswordWithTheStoresReason()
    {
        var connection = SavedConnection("1Password CLI");
        _secrets.Lookups["1Password CLI"] = SecretLookup.Unavailable("1Password CLI couldn't read the password: You are not signed in.");

        await _sut.InitializeAsync();

        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
        connection.LastError.ShouldBe("1Password CLI couldn't read the password: You are not signed in.");
    }

    [Fact]
    public async Task Startup_ConnectionWithoutAPassword_ConnectsWithoutReadingAStore()
    {
        var connection = NewConnection(WithoutPassword);
        _connectionService.GetSavedConnections().Returns([connection]);

        await _sut.InitializeAsync();

        connection.Active.ShouldBeTrue();
        _secrets.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_NeedsPassword_DoesNotTryToLogIn()
    {
        var connection = SavedConnection(passwordStore: null);
        await _sut.InitializeAsync();

        var result = await _sut.RefreshDatabaseAsync(connection);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe(ConnectionState.PasswordNotStoredReason);
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
        await _connectionService.DidNotReceiveWithAnyArgs().GetDatabasesAsync(default!, default);
    }

    // Password prompt

    [Fact]
    public async Task EnterPassword_DontStore_ConnectsForThisSessionOnly()
    {
        var connection = SavedConnection(passwordStore: null);
        await _sut.InitializeAsync();

        var result = await _sut.ConnectWithPasswordAsync(connection.Id, "hunter2", PasswordChoice.DontStore);

        result.Success.ShouldBeTrue();
        connection.Active.ShouldBeTrue();
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.Healthy);
        ConnectionPasswords.GetPassword(DatabaseType.PostgreSQL, connection.ConnectionString).ShouldBe("hunter2");
        connection.Databases.Select(d => d.Name).ShouldBe(["app"]);
        _secrets.Passwords.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnterPassword_Store_SavesAndRecordsTheStore()
    {
        var connection = SavedConnection(passwordStore: null);
        await _sut.InitializeAsync();

        await _sut.ConnectWithPasswordAsync(connection.Id, "hunter2", PasswordChoice.Store);

        _secrets.Passwords[(ConnectionSecretStoreFake.Vault, connection.Id)].ShouldBe("hunter2");
        connection.PasswordStore.ShouldBe(ConnectionSecretStoreFake.Vault);
        await _connectionService.Received(1).UpdateConnection(connection);
    }

    [Fact]
    public async Task EnterPassword_ServerRejects_StillNeedsThePassword()
    {
        var connection = SavedConnection(passwordStore: null);
        await _sut.InitializeAsync();
        ServerRejects("password authentication failed for user \"app\"");

        var result = await _sut.ConnectWithPasswordAsync(connection.Id, "wrong", PasswordChoice.Store);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("password authentication failed for user \"app\"");
        connection.HealthStatus.ShouldBe(ConnectionHealthStatus.NeedsPassword);
        connection.ConnectionString.ShouldBe(WithoutPassword);
        _secrets.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnterPassword_ForAConnectionThatIsGone_Fails()
    {
        var result = await _sut.ConnectWithPasswordAsync(Guid.NewGuid(), "hunter2", PasswordChoice.Store);

        result.Success.ShouldBeFalse();
    }
}
