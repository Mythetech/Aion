using Aion.Components;
using Aion.Components.Infrastructure;
using Aion.Components.NativeMenu;
using Aion.Components.Querying;
using Aion.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;

using WebApp = Aion.Web.App;
using ComponentsApp = Aion.Components.App;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<WebApp>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAionComponents<WebConnectionService>();

builder.Services.AddSettingsStorage<InMemorySettingsStorage>();

builder.Services.AddSingleton<IQuerySaveService, InMemoryQuerySaveService>();
builder.Services.AddTransient<ILinkOpenService, BrowserLinkOpenService>();
builder.Services.AddSingleton<INativeMenuService, NoOpNativeMenuService>();

builder.Services.AddMessageBus(typeof(WebApp).Assembly, typeof(ComponentsApp).Assembly);

var host = builder.Build();

host.Services.UseMessageBus(typeof(WebApp).Assembly, typeof(ComponentsApp).Assembly);

await host.RunAsync();
