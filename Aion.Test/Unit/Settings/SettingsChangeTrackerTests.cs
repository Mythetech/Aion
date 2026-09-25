using Aion.Components.Settings;
using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;
using Shouldly;

namespace Aion.Test.Unit.Settings;

public class SettingsChangeTrackerTests
{
    private readonly EditorSettings _editor = new();
    private readonly AppearanceSettings _appearance = new();
    private SettingsBase[] All => [_editor, _appearance];

    [Fact]
    public void TakeChanged_NothingEdited_ReturnsNothing()
    {
        var tracker = new SettingsChangeTracker(All);

        tracker.TakeChanged(All).ShouldBeEmpty();
    }

    [Fact]
    public void TakeChanged_ReturnsOnlyTheEditedSection()
    {
        var tracker = new SettingsChangeTracker(All);

        _editor.FontSize = 20;

        tracker.TakeChanged(All).ShouldBe([_editor]);
    }

    [Fact]
    public void TakeChanged_DoesNotReturnASectionTwiceForOneEdit()
    {
        var tracker = new SettingsChangeTracker(All);
        _appearance.Theme = ThemeMode.Dark;
        tracker.TakeChanged(All);

        tracker.TakeChanged(All).ShouldBeEmpty();
    }

    [Fact]
    public void TakeChanged_EditingBackToTheOriginalValue_IsStillAChange()
    {
        var tracker = new SettingsChangeTracker(All);
        _editor.WordWrap = true;
        tracker.TakeChanged(All);

        _editor.WordWrap = false;

        tracker.TakeChanged(All).ShouldBe([_editor]);
    }
}
