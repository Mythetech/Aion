using SqliteWasmBlazor;

namespace Aion.Web.Services;

public interface ISqliteWasmInitializer
{
    Task InitializeAsync();
}

public class SqliteWasmInitializer : ISqliteWasmInitializer
{
    private readonly IServiceProvider _serviceProvider;

    public SqliteWasmInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task InitializeAsync()
    {
        await _serviceProvider.InitializeSqliteWasmAsync();
    }
}
