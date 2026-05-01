using Aion.Components.Connections;
using Aion.Components.Querying;
using Aion.Contracts.Connections;
using Aion.Contracts.Database;
using Aion.Contracts.Queries;
using Microsoft.Extensions.Logging;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit;

public class SqlCompletionServiceTests
{
    private readonly SqlCompletionService _sut;
    private readonly ConnectionState _connectionState;

    public SqlCompletionServiceTests()
    {
        var messageBus = Substitute.For<IMessageBus>();
        var providerFactory = Substitute.For<IDatabaseProviderFactory>();
        var connectionService = Substitute.For<IConnectionService>();
        var logger = Substitute.For<ILogger<ConnectionState>>();
        _connectionState = new ConnectionState(connectionService, providerFactory, messageBus, logger);
        _sut = new SqlCompletionService(_connectionState);
    }

    #region Keyword Fallback

    [Fact]
    public async Task GetCompletionsAsync_NoConnection_ReturnsKeywords()
    {
        var result = await _sut.GetCompletionsAsync("SEL", 0, 3, null, null, null);

        result.ShouldNotBeEmpty();
        result.ShouldAllBe(item => item.Kind == SqlCompletionKind.Keyword);
        result.ShouldContain(item => item.Label == "SELECT");
    }

    [Fact]
    public async Task GetCompletionsAsync_EmptyText_ReturnsKeywords()
    {
        var result = await _sut.GetCompletionsAsync("", 0, 0, null, null, null);

        result.ShouldNotBeEmpty();
        result.ShouldAllBe(item => item.Kind == SqlCompletionKind.Keyword);
    }

    [Fact]
    public async Task GetCompletionsAsync_Keywords_HaveCorrectSortPrefix()
    {
        var result = await _sut.GetCompletionsAsync("", 0, 0, null, null, null);

        result.ShouldAllBe(item => item.SortText != null && item.SortText.StartsWith("4_"));
    }

    #endregion

    #region Table Completions

    [Fact]
    public async Task GetCompletionsAsync_AfterFrom_ReturnsTableCompletions()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM ", 0, 14, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table && item.Label == "users");
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table && item.Label == "orders");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterJoin_ReturnsTableCompletions()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users JOIN ", 0, 25, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table && item.Label == "orders");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterLeftJoin_ReturnsTableCompletions()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users LEFT JOIN ", 0, 30, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table);
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterUpdate_ReturnsTableCompletions()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("UPDATE ", 0, 7, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table && item.Label == "users");
    }

    [Fact]
    public async Task GetCompletionsAsync_SingleSchema_NoPrefix()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM ", 0, 14, null, connectionId, dbName);

        var tableItems = result.Where(i => i.Kind == SqlCompletionKind.Table).ToList();
        tableItems.ShouldAllBe(item => !item.Label.Contains('.'));
    }

    [Fact]
    public async Task GetCompletionsAsync_MultipleSchemas_UsesPrefix()
    {
        var (connectionId, dbName) = SetupMultiSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM ", 0, 14, null, connectionId, dbName);

        var tableItems = result.Where(i => i.Kind == SqlCompletionKind.Table).ToList();
        tableItems.ShouldContain(item => item.Label == "public.users");
        tableItems.ShouldContain(item => item.Label == "sales.orders");
    }

    [Fact]
    public async Task GetCompletionsAsync_CommaAfterFrom_ReturnsTableCompletions()
    {
        var (connectionId, dbName) = SetupSingleSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users,", 0, 20, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Table);
    }

    #endregion

    #region Column Completions

    [Fact]
    public async Task GetCompletionsAsync_AfterSelect_WithTableRef_ReturnsColumns()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT \nFROM users", 0, 7, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Column && item.Label == "id");
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Column && item.Label == "name");
    }

    [Fact]
    public async Task GetCompletionsAsync_AfterWhere_WithTableRef_ReturnsColumns()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users WHERE ", 0, 26, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Column && item.Label == "id");
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Column && item.Label == "name");
    }

    [Fact]
    public async Task GetCompletionsAsync_ColumnDetail_IncludesTypeAndFlags()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users WHERE ", 0, 26, null, connectionId, dbName);

        var idColumn = result.FirstOrDefault(i => i.Label == "id");
        idColumn.ShouldNotBeNull();
        idColumn.Detail.ShouldContain("integer");
        idColumn.Detail.ShouldContain("PK");

        var nameColumn = result.FirstOrDefault(i => i.Label == "name");
        nameColumn.ShouldNotBeNull();
        nameColumn.Detail.ShouldContain("varchar");
        nameColumn.Detail.ShouldContain("nullable");
        nameColumn.Detail.ShouldContain("max: 255");
    }

    #endregion

    #region Dot-Qualified Completions

    [Fact]
    public async Task GetCompletionsAsync_SchemaQualified_ReturnsSchemaTables()
    {
        var (connectionId, dbName) = SetupMultiSchemaDatabase();

        var result = await _sut.GetCompletionsAsync("SELECT * FROM public.", 0, 21, ".", connectionId, dbName);

        result.ShouldAllBe(item => item.Kind == SqlCompletionKind.Table);
        result.ShouldContain(item => item.Label == "users");
        result.ShouldNotContain(item => item.Label == "orders");
    }

    [Fact]
    public async Task GetCompletionsAsync_TableQualified_ReturnsTableColumns()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT users. FROM users", 0, 13, ".", connectionId, dbName);

        result.ShouldAllBe(item => item.Kind == SqlCompletionKind.Column);
        result.ShouldContain(item => item.Label == "id");
        result.ShouldContain(item => item.Label == "name");
    }

    [Fact]
    public async Task GetCompletionsAsync_AliasQualified_ReturnsTableColumns()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT u. FROM users u", 0, 9, ".", connectionId, dbName);

        result.ShouldAllBe(item => item.Kind == SqlCompletionKind.Column);
        result.ShouldContain(item => item.Label == "id");
        result.ShouldContain(item => item.Label == "name");
    }

    [Fact]
    public async Task GetCompletionsAsync_UnknownDotQualified_ReturnsEmpty()
    {
        var (connectionId, dbName) = SetupDatabaseWithColumns();

        var result = await _sut.GetCompletionsAsync("SELECT unknown.", 0, 15, ".", connectionId, dbName);

        // Falls through to keywords since dot-qualified returned nothing
        result.ShouldNotContain(item => item.Kind == SqlCompletionKind.Table);
        result.ShouldNotContain(item => item.Kind == SqlCompletionKind.Column);
    }

    #endregion

    #region Routine Completions

    [Fact]
    public async Task GetCompletionsAsync_WithRoutines_ReturnsFunctions()
    {
        var (connectionId, dbName) = SetupDatabaseWithRoutines();

        var result = await _sut.GetCompletionsAsync("SELECT ", 0, 7, null, connectionId, dbName);

        var funcItem = result.FirstOrDefault(i => i.Label == "calculate_tax");
        funcItem.ShouldNotBeNull();
        funcItem.Kind.ShouldBe(SqlCompletionKind.Function);
        funcItem.Detail.ShouldBe("returns numeric");
    }

    [Fact]
    public async Task GetCompletionsAsync_WithRoutines_ReturnsProcedures()
    {
        var (connectionId, dbName) = SetupDatabaseWithRoutines();

        var result = await _sut.GetCompletionsAsync("SELECT ", 0, 7, null, connectionId, dbName);

        var procItem = result.FirstOrDefault(i => i.Label == "refresh_cache");
        procItem.ShouldNotBeNull();
        procItem.Kind.ShouldBe(SqlCompletionKind.Procedure);
        procItem.Detail.ShouldBe("(interval_seconds integer)");
    }

    [Fact]
    public async Task GetCompletionsAsync_RoutinesInUnknownContext_AreIncluded()
    {
        var (connectionId, dbName) = SetupDatabaseWithRoutines();

        var result = await _sut.GetCompletionsAsync("EXEC ", 0, 5, null, connectionId, dbName);

        result.ShouldContain(item => item.Kind == SqlCompletionKind.Function);
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Procedure);
    }

    #endregion

    #region Eager Loading / Safety

    [Fact]
    public async Task GetCompletionsAsync_TablesNotLoaded_DoesNotCrash()
    {
        var connectionId = Guid.NewGuid();
        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [new DatabaseModel { Name = "testdb", TablesLoaded = false }]
        };
        _connectionState.Connections.Add(connection);

        var result = await _sut.GetCompletionsAsync("SELECT * FROM ", 0, 14, null, connectionId, "testdb");

        result.ShouldNotBeNull();
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Keyword);
    }

    [Fact]
    public async Task GetCompletionsAsync_ColumnsNotLoaded_DoesNotCrash()
    {
        var connectionId = Guid.NewGuid();
        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases =
            [
                new DatabaseModel
                {
                    Name = "testdb",
                    TablesLoaded = true,
                    Tables = [new TableInfo("public", "users")]
                }
            ]
        };
        _connectionState.Connections.Add(connection);

        var result = await _sut.GetCompletionsAsync("SELECT * FROM users WHERE ", 0, 26, null, connectionId, "testdb");

        result.ShouldNotBeNull();
        result.ShouldContain(item => item.Kind == SqlCompletionKind.Keyword);
    }

    #endregion

    #region Helpers

    private (Guid connectionId, string dbName) SetupSingleSchemaDatabase()
    {
        var connectionId = Guid.NewGuid();
        var database = new DatabaseModel
        {
            Name = "testdb",
            TablesLoaded = true,
            Tables =
            [
                new TableInfo("public", "users"),
                new TableInfo("public", "orders")
            ]
        };

        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [database]
        };
        _connectionState.Connections.Add(connection);
        return (connectionId, "testdb");
    }

    private (Guid connectionId, string dbName) SetupMultiSchemaDatabase()
    {
        var connectionId = Guid.NewGuid();
        var database = new DatabaseModel
        {
            Name = "testdb",
            TablesLoaded = true,
            Tables =
            [
                new TableInfo("public", "users"),
                new TableInfo("sales", "orders")
            ]
        };

        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [database]
        };
        _connectionState.Connections.Add(connection);
        return (connectionId, "testdb");
    }

    private (Guid connectionId, string dbName) SetupDatabaseWithColumns()
    {
        var connectionId = Guid.NewGuid();
        var database = new DatabaseModel
        {
            Name = "testdb",
            TablesLoaded = true,
            Tables = [new TableInfo("public", "users")],
            TableColumns = new Dictionary<string, List<ColumnInfo>>
            {
                ["public.users"] =
                [
                    new ColumnInfo { Name = "id", DataType = "integer", IsPrimaryKey = true },
                    new ColumnInfo { Name = "name", DataType = "varchar", IsNullable = true, MaxLength = 255 },
                    new ColumnInfo { Name = "email", DataType = "varchar", IsNullable = false }
                ]
            },
            LoadedColumnTables = ["public.users"]
        };

        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [database]
        };
        _connectionState.Connections.Add(connection);
        return (connectionId, "testdb");
    }

    private (Guid connectionId, string dbName) SetupDatabaseWithRoutines()
    {
        var connectionId = Guid.NewGuid();
        var database = new DatabaseModel
        {
            Name = "testdb",
            TablesLoaded = true,
            Tables = [new TableInfo("public", "users")],
            RoutinesLoaded = true,
            Routines =
            [
                new RoutineInfo("public", "calculate_tax", RoutineKind.Function, "numeric", "(amount numeric)", "sql"),
                new RoutineInfo("public", "refresh_cache", RoutineKind.Procedure, null, "(interval_seconds integer)", "plpgsql")
            ]
        };

        var connection = new ConnectionModel
        {
            Id = connectionId,
            Name = "Test",
            ConnectionString = "Server=localhost",
            Type = DatabaseType.PostgreSQL,
            Active = true,
            Databases = [database]
        };
        _connectionState.Connections.Add(connection);
        return (connectionId, "testdb");
    }

    #endregion
}
