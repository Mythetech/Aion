using Aion.Components;
using Aion.Components.Shortcuts;
using Aion.Test.TestDoubles;
using BlazorMonaco;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Utilities;
using Mythetech.Framework.Infrastructure.MessageBus;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Shortcuts;

public class AionKeyBindingsTests
{
    private static IEnumerable<KeyChord> AllChords(AionKeyBindings bindings) => new[]
    {
        bindings.RunQuery, bindings.CommandPalette, bindings.CommandPaletteAlternate, bindings.ExpandSelectStar,
        bindings.SetNull, bindings.NewQuery, bindings.NewQueryAlternate, bindings.NewConnection, bindings.SaveQuery, bindings.CopyQuery
    }.OfType<KeyChord>();

    [Fact]
    public void Desktop_NewQueryIsCmdN_AndNewConnectionMovesToShiftCmdN()
    {
        // Arrange
        var bindings = AionKeyBindings.ForDesktop(isMac: true);

        // Assert
        bindings.NewQuery!.Accelerator.ShouldBe("Ctrl+N");
        bindings.NewConnection!.Accelerator.ShouldBe("Ctrl+Shift+N");
        bindings.SaveQuery!.Accelerator.ShouldBe("Ctrl+S");
        bindings.Describe(bindings.NewQuery).ShouldBe("⌘N");
        bindings.Describe(bindings.NewConnection).ShouldBe("⇧⌘N");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NoChordDoesTwoThings(bool browser)
    {
        // Arrange
        var bindings = browser ? AionKeyBindings.ForBrowser(isMac: false) : AionKeyBindings.ForDesktop(isMac: false);

        // Assert
        AllChords(bindings).ShouldBeUnique();
    }

    [Fact]
    public void Browser_DoesNotClaimShortcutsTheBrowserKeeps()
    {
        // Arrange
        var bindings = AionKeyBindings.ForBrowser(isMac: true);

        // Assert
        bindings.NewQuery.ShouldBeNull();
        bindings.NewQueryAlternate.ShouldBeNull();
        bindings.NewConnection.ShouldBeNull();
        bindings.SaveQuery.ShouldBeNull();
        bindings.Describe(bindings.NewQuery).ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditorShortcuts_AreTheSameInBothHosts(bool isMac)
    {
        // Arrange
        var desktop = AionKeyBindings.ForDesktop(isMac);
        var browser = AionKeyBindings.ForBrowser(isMac);

        // Assert
        browser.RunQuery.ShouldBe(desktop.RunQuery);
        browser.CommandPalette.ShouldBe(desktop.CommandPalette);
        browser.CommandPaletteAlternate.ShouldBe(desktop.CommandPaletteAlternate);
        browser.ExpandSelectStar.ShouldBe(desktop.ExpandSelectStar);
    }

    [Fact]
    public void Registration_ResolvesFromTheRootProviderWithScopeValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IMessageBus>());
        services.AddAionComponents<ConnectionServiceFake>();

        // Act
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var bindings = provider.GetRequiredService<AionKeyBindings>();

        // Assert
        bindings.NewQuery.ShouldNotBeNull();
    }

    [Fact]
    public void Chords_TranslateForEachConsumer()
    {
        // Arrange
        var run = new KeyChord("Enter");
        var palette = new KeyChord("P", Shift: true);

        // Assert
        run.EditorKeybinding.ShouldBe((int)KeyMod.CtrlCmd | (int)KeyCode.Enter);
        palette.EditorKeybinding.ShouldBe((int)KeyMod.CtrlCmd | (int)KeyMod.Shift | (int)KeyCode.KeyP);
        palette.HotkeyKey.ShouldBe(JsKey.KeyP);
        palette.HotkeyModifiers.ShouldBe(
        [
            [JsKeyModifier.ControlLeft, JsKeyModifier.ShiftLeft],
            [JsKeyModifier.MetaLeft, JsKeyModifier.ShiftLeft]
        ]);
        run.Display(mac: true).ShouldBe("⌘↵");
        run.Display(mac: false).ShouldBe("Ctrl+Enter");
        palette.Display(mac: false).ShouldBe("Ctrl+Shift+P");
    }

    [Fact]
    public void DigitChords_TranslateToDigitKeys()
    {
        var setNull = AionKeyBindings.ForBrowser(isMac: true).SetNull;

        setNull.EditorKeybinding.ShouldBe((int)KeyMod.CtrlCmd | (int)KeyCode.Digit0);
        setNull.HotkeyKey.ShouldBe(JsKey.Digit0);
        setNull.Display(mac: true).ShouldBe("⌘0");
        setNull.Display(mac: false).ShouldBe("Ctrl+0");
    }
}
