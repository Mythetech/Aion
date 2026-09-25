using System.Text.Json;
using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Aion.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.Infrastructure.Settings.Consumers;
using Mythetech.Framework.Infrastructure.Settings.Events;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Web;

public class IndexedDbSettingsStorageTests
{
    // Stands in for the "settings" object store that aion-storage.js keeps in IndexedDB.
    private readonly Dictionary<string, string> _objectStore = new();

    private IndexedDbSettingsStorage CreateStorage()
    {
        var module = Substitute.For<IJSObjectReference>();

        module.InvokeAsync<IJSVoidResult>("requestPersistence", Arg.Any<object?[]?>())
            .Returns(_ => ValueTask.FromResult<IJSVoidResult>(null!));

        module.InvokeAsync<IJSVoidResult>("saveSettings", Arg.Any<object?[]?>())
            .Returns(call =>
            {
                var args = call.ArgAt<object?[]>(1);
                _objectStore[(string)args[0]!] = (string)args[1]!;
                return ValueTask.FromResult<IJSVoidResult>(null!);
            });

        module.InvokeAsync<string>("loadSettings", Arg.Any<object?[]?>())
            .Returns(call => ValueTask.FromResult(_objectStore.GetValueOrDefault((string)call.ArgAt<object?[]>(1)[0]!)!));

        module.InvokeAsync<string>("loadAllSettings", Arg.Any<object?[]?>())
            .Returns(_ => ValueTask.FromResult(JsonSerializer.Serialize(
                _objectStore.Select(entry => new { settingsId = entry.Key, json = entry.Value }))));

        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]?>())
            .Returns(ValueTask.FromResult(module));

        return new IndexedDbSettingsStorage(new IndexedDbStorageService(js));
    }

    [Fact]
    public async Task SavedSettings_LoadBack()
    {
        var storage = CreateStorage();

        await storage.SaveSettingsAsync("Editor", """{"FontSize":20}""");
        await storage.SaveSettingsAsync("Appearance", """{"Theme":1}""");

        (await storage.LoadSettingsAsync("Editor")).ShouldBe("""{"FontSize":20}""");
        (await storage.LoadAllSettingsAsync()).ShouldBe(new Dictionary<string, string>
        {
            ["Editor"] = """{"FontSize":20}""",
            ["Appearance"] = """{"Theme":1}""",
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheStoredValue()
    {
        var storage = CreateStorage();

        await storage.SaveSettingsAsync("Editor", """{"FontSize":20}""");
        await storage.SaveSettingsAsync("Editor", """{"FontSize":12}""");

        (await storage.LoadSettingsAsync("Editor")).ShouldBe("""{"FontSize":12}""");
        (await storage.LoadAllSettingsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnsavedSection_LoadsAsNothing()
    {
        var storage = CreateStorage();

        (await storage.LoadSettingsAsync("Editor")).ShouldBeNull();
        (await storage.LoadAllSettingsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ThemeAndFontSize_SurviveAReload()
    {
        var storage = CreateStorage();
        var persister = new SettingsPersister(NullLogger<SettingsPersister>.Instance, storage);
        await persister.Consume(new SettingsModelChanged(new AppearanceSettings { Theme = ThemeMode.Dark }));
        await persister.Consume(new SettingsModelChanged(new EditorSettings { FontSize = 20 }));

        var appearance = new AppearanceSettings();
        var editor = new EditorSettings();
        var afterReload = new SettingsProvider(
            Substitute.For<IMessageBus>(),
            NullLogger<SettingsProvider>.Instance,
            Substitute.For<IServiceProvider>(),
            Options.Create(new SettingsRegistrationOptions()));
        afterReload.RegisterSettings(appearance);
        afterReload.RegisterSettings(editor);
        await afterReload.ApplyPersistedSettingsAsync(await storage.LoadAllSettingsAsync());

        appearance.Theme.ShouldBe(ThemeMode.Dark);
        editor.FontSize.ShouldBe(20);
    }
}
