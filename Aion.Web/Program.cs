using Aion.Components;
using Aion.Components.Connections;
using Aion.Components.Infrastructure;
using Aion.Components.NativeMenu;
using Aion.Components.Querying;
using Aion.Contracts.Database;
using Aion.Web.Databases;
using Aion.Web.Providers;
using Aion.Web.Onboarding;
using Aion.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Guards;
using Mythetech.Framework.Infrastructure.Plugins;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.WebAssembly;
using SqliteWasmBlazor;

using WebApp = Aion.Web.App;
using ComponentsApp = Aion.Components.App;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<WebApp>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSqliteWasm(o => o.HostEnvironment = builder.HostEnvironment);

builder.Services.AddAionComponents<WebConnectionService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDatabaseProviderFactory, DatabaseProviderFactory>();
builder.Services.AddJsGuards();

builder.Services.AddSettingsStorage<InMemorySettingsStorage>();
builder.Services.AddWebAssemblyServices();

builder.Services.AddSingleton<IQuerySaveService, IndexedDbQuerySaveService>();
builder.Services.AddSingleton<IndexedDbStorageService>();
builder.Services.AddSingleton<INativeMenuService, NoOpNativeMenuService>();

builder.Services.AddSingleton<SqliteWasmProvider>();
builder.Services.AddSingleton<IDatabaseProvider>(sp => sp.GetRequiredService<SqliteWasmProvider>());
builder.Services.AddSingleton<PGliteProvider>();
builder.Services.AddSingleton<IDatabaseProvider>(sp => sp.GetRequiredService<PGliteProvider>());

builder.Services.AddSingleton<ISupportedTypeProvider, SqliteWasmTypeProvider>();
builder.Services.AddSingleton<ISupportedTypeProvider, PGliteTypeProvider>();
builder.Services.AddSingleton<SchemaExecutor>();
builder.Services.AddSingleton<SampleDatabaseProvisioner>();
builder.Services.AddSingleton<BrowserStorageCleaner>();
builder.Services.AddSingleton<ISqliteWasmInitializer, SqliteWasmInitializer>();
builder.Services.AddSingleton<StorageRestoreService>();
builder.Services.AddSingleton<IConnectionPrompt, BrowserConnectionPrompt>();

builder.Services.AddMessageBus(typeof(WebApp).Assembly, typeof(ComponentsApp).Assembly);

// AddMessageBus re-registers IConsumer<T> types as Transient; restore the stateful ones as
// singletons so the bus and every component share one instance
builder.Services.AddSingleton<QueryState>();
builder.Services.AddSingleton<WebPersistenceManager>();

var host = builder.Build();

host.Services.UseMessageBus(typeof(WebApp).Assembly, typeof(ComponentsApp).Assembly);
host.Services.UseSettingsFramework();
host.Services.UsePluginFramework();

var persistenceManager = host.Services.GetRequiredService<WebPersistenceManager>();
persistenceManager.Start();

await host.RunAsync();
