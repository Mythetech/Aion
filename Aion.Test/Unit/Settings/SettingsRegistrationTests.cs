using Aion.Components;
using Aion.Components.Settings;
using Aion.Components.Settings.Domains;
using Aion.Test.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Settings;

public class SettingsRegistrationTests
{
    private static IReadOnlyList<SettingsBase> ListedSections(Action<IServiceCollection>? hostRegistration = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IMessageBus>());
        services.AddAionComponents<ConnectionServiceFake>();
        hostRegistration?.Invoke(services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISettingsProvider>().GetAllSettings();
    }

    [Fact]
    public void WebHost_ListsOnlySharedSections()
    {
        var ids = ListedSections().Select(s => s.SettingsId);

        ids.ShouldBe(["Appearance", "Browser", "Editor"]);
    }

    [Fact]
    public void DesktopHost_AddsConnectionsPluginsAndPrivacy()
    {
        var ids = ListedSections(services => services.AddAionDesktopSettings()).Select(s => s.SettingsId).ToList();

        ids.ShouldContain("Appearance");
        ids.ShouldContain("Browser");
        ids.ShouldContain("Editor");
        ids.ShouldContain("Connections");
        ids.ShouldContain("Plugins");
        ids.ShouldContain("privacy");
    }

    [Fact]
    public void McpServerSection_IsNotListedOnAnyHost()
    {
        ListedSections().ShouldNotContain(s => s.SettingsId == "Mcp");
        ListedSections(services => services.AddAionDesktopSettings()).ShouldNotContain(s => s.SettingsId == "Mcp");
    }

    [Fact]
    public void WebHost_StillProvidesConnectionSettingsToTheHealthMonitor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IMessageBus>());
        services.AddAionComponents<ConnectionServiceFake>();

        using var provider = services.BuildServiceProvider();

        provider.GetService<ConnectionSettings>().ShouldNotBeNull();
    }

    [Fact]
    public void SchemaTreeSection_IsNamedSchemaExplorer()
    {
        new BrowserSettings().DisplayName.ShouldBe("Schema explorer");
    }
}
