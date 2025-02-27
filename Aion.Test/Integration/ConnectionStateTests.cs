using Aion.Components.Connections;
using Aion.Components.Connections.Commands;
using Aion.Components.Connections.Consumers;
using Aion.Components.Infrastructure.MessageBus;
using Aion.Components.Querying;
using Aion.Components.Querying.Events;
using Aion.Core.Connections;
using Aion.Core.Database;
using Aion.Core.Queries;
using Aion.Desktop;
using Bunit;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Integration;

public abstract class ConnectionStateTestBase : TestContext, IAsyncLifetime
{
    protected readonly ConnectionState ConnectionState;
    protected readonly IMessageBus MessageBus;
    protected readonly QueryModel TestQuery;
    protected abstract IDatabaseProvider Provider { get;   }
    protected abstract string ConnectionString { get;  }
    
    protected const string TestDatabase = "aion_test_db";
    protected const string TestTable = "test_table";

    protected ConnectionStateTestBase()
    {
        var logFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });
        MessageBus = new InMemoryMessageBus(base.Services, logFactory.CreateLogger<InMemoryMessageBus>());
        var providerFactory = new DatabaseProviderFactory([Provider]);
        var logger = new Logger<ConnectionState>(logFactory);
        var service = new ConnectionService(providerFactory, Substitute.For<IConnectionStorage>());
        ConnectionState = new ConnectionState(service, providerFactory, MessageBus, logger);
        
        TestQuery = new QueryModel
        {
            Name = "Test Query",
            DatabaseName = "aion_test_db",
            Query = "SELECT * FROM test_table"
        };
        
        MessageBus.RegisterConsumerType<StartTransaction, TransactionInitializer>();
        MessageBus.RegisterConsumerType<CommitTransaction, TransactionFinalizer>();
        MessageBus.RegisterConsumerType<RollbackTransaction, TransactionFinalizer>();

        Services.AddSingleton<TransactionInitializer>();
        Services.AddSingleton<TransactionFinalizer>();
        Services.AddSingleton<ConnectionState>(ConnectionState);
        Services.AddSingleton(MessageBus);
    }

    [Fact(Skip = "Feature in progress")]
    public async Task ExecuteQuery_WithoutTransaction_ShouldReturnResults()
    {
        // Arrange
        var connection = new ConnectionModel()
        {
            Type = Provider.DatabaseType,
            ConnectionString = ConnectionString,
            Name = "Test Connection"
        };
        await ConnectionState.ConnectAsync(connection);
        
        var seedQuery = new QueryModel()
        {
            Query = "INSERT INTO test_table (name) VALUES ('test')",
            Name = "Test Query",
            DatabaseName = TestDatabase,
            ConnectionId = connection.Id,
        };
        var insertResult = await ConnectionState.ExecuteQueryAsync(seedQuery, CancellationToken.None);
        insertResult.ShouldNotBeNull();
        insertResult.Error.ShouldBeNull();
        
        TestQuery.ConnectionId = connection.Id;

        // Act
        var result = await ConnectionState.ExecuteQueryAsync(TestQuery, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
        result.Rows.Count.ShouldBeGreaterThan(0);
    }

    [Fact(Skip = "Feature in progress")]
    public async Task ExecuteQuery_WithTransaction_ShouldMaintainTransactionState()
    {
        // Arrange
        var connection = new ConnectionModel()
        {
            Type = Provider.DatabaseType,
            ConnectionString = ConnectionString,
        };
        await ConnectionState.ConnectAsync(connection);
        
        // Seed initial data
        var seedQuery = new QueryModel()
        {
            Query = "INSERT INTO test_table (name) VALUES ('initial')",
            Name = "Seed Query",
            DatabaseName = TestDatabase,
            ConnectionId = connection.Id,
        };
        var seedResult = await ConnectionState.ExecuteQueryAsync(seedQuery, CancellationToken.None);
        seedResult.ShouldNotBeNull();
        seedResult.Error.ShouldBeNull();
        
        // Set up test query
        TestQuery.ConnectionId = connection.Id;
        TestQuery.DatabaseName = TestDatabase;
        TestQuery.UseTransaction = true;

        // Act
        var result1 = await ConnectionState.ExecuteQueryAsync(TestQuery, CancellationToken.None);
        result1.ShouldNotBeNull();
        result1.Error.ShouldBeNull();
        TestQuery.Transaction.ShouldNotBeNull();
        TestQuery.Transaction.Value.Status.ShouldBe(TransactionStatus.Active);

        TestQuery.Query = "INSERT INTO test_table (name) VALUES ('test')";
        var result2 = await ConnectionState.ExecuteQueryAsync(TestQuery, CancellationToken.None);
        result2.ShouldNotBeNull();
        result2.Error.ShouldBeNull();
        TestQuery.Transaction.ShouldNotBeNull();
        TestQuery.Transaction.Value.Status.ShouldBe(TransactionStatus.Active);

        await MessageBus.PublishAsync(new RollbackTransaction(TestQuery));
        TestQuery.Transaction.ShouldNotBeNull();
        TestQuery.Transaction.Value.Status.ShouldBe(TransactionStatus.RolledBack);

        // Verify rollback worked
        TestQuery.UseTransaction = false;
        TestQuery.Query = "SELECT COUNT(*) as count FROM test_table WHERE name = 'test'";
        var finalResult = await ConnectionState.ExecuteQueryAsync(TestQuery, CancellationToken.None);
        finalResult.ShouldNotBeNull();
        finalResult.Error.ShouldBeNull();
        finalResult.Rows[0]["count"].ToString().ShouldBe("0");
    }

    [Fact(Skip = "Feature in progress")]
    public async Task ExecuteQuery_WithEstimatedPlan_ShouldReturnPlan()
    {
        // Arrange
        var connection = new ConnectionModel()
        {
            Type = Provider.DatabaseType,
            ConnectionString = ConnectionString,
        };
        await ConnectionState.ConnectAsync(connection);
        
        // Set up test query
        var seedQuery = new QueryModel()
        {
            Query = "INSERT INTO test_table (name) VALUES ('initial')",
            Name = "Seed Query",
            DatabaseName = TestDatabase,
            ConnectionId = connection.Id,
        };
        await ConnectionState.ExecuteQueryAsync(seedQuery, CancellationToken.None);
        
        TestQuery.ConnectionId = connection.Id;
        TestQuery.DatabaseName = TestDatabase;
        TestQuery.IncludeEstimatedPlan = true;
        TestQuery.Query = "SELECT * FROM test_table WHERE name = 'test'";

        // Act
        var result = await ConnectionState.ExecuteQueryAsync(TestQuery, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
        TestQuery.EstimatedPlan.ShouldNotBeNull();
        TestQuery.EstimatedPlan.PlanContent.ShouldNotBeEmpty();
    }

    protected virtual async Task SetupTestDatabase()
    {
        var createDbScript = await Provider.Commands.GenerateCreateDatabaseScript(TestDatabase);

        if (Provider.DatabaseType == DatabaseType.SQLServer)
        {
            var masterConnection = Provider.UpdateConnectionString(ConnectionString, "master");
            await Provider.ExecuteQueryAsync(masterConnection, createDbScript, CancellationToken.None);
        }
        else
        {
            await Provider.ExecuteQueryAsync(ConnectionString, createDbScript, CancellationToken.None);
        }

        var dbConnectionString = Provider.UpdateConnectionString(ConnectionString, TestDatabase);

        if (Provider.DatabaseType == DatabaseType.MySQL)
        {
            await Task.Delay(1000);
        }

        var createTableScript = await Provider.Commands.GenerateCreateTableScript(
            TestDatabase,
            TestTable,
            new[]
            {
                new ColumnDefinition("id", Provider.DatabaseType switch
                {
                    DatabaseType.SQLServer => "int IDENTITY(1,1)",
                    DatabaseType.MySQL => "int AUTO_INCREMENT",
                    DatabaseType.PostgreSQL => "SERIAL",
                    _ => throw new NotSupportedException($"Unsupported database type: {Provider.DatabaseType}")
                }, false),
                new ColumnDefinition("name", "varchar(100)", false),
                new ColumnDefinition("description", "text", true)
            });
        await Provider.ExecuteQueryAsync(dbConnectionString, createTableScript, CancellationToken.None);
    }
    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;
}