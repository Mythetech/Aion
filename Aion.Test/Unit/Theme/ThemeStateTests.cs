using Aion.Components.Settings.Domains;
using Aion.Components.Theme;
using Mythetech.Framework.Infrastructure.Settings;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Theme;

public class ThemeStateTests
{
    private readonly AppearanceSettings _settings = new();
    private readonly ISettingsProvider _settingsProvider = Substitute.For<ISettingsProvider>();

    private ThemeState CreateState(ThemeMode mode)
    {
        _settings.Theme = mode;
        return new ThemeState(_settings, _settingsProvider);
    }

    [Theory]
    [InlineData(ThemeMode.Light, false, false)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData(ThemeMode.Dark, true, true)]
    [InlineData(ThemeMode.System, false, false)]
    [InlineData(ThemeMode.System, true, true)]
    public void Resolve_UsesTheOsPreferenceOnlyInSystemMode(ThemeMode mode, bool systemPrefersDark, bool expectedDark)
    {
        ThemeState.Resolve(mode, systemPrefersDark).ShouldBe(expectedDark);
    }

    [Fact]
    public void SystemMode_FollowsReportedOsPreference()
    {
        var state = CreateState(ThemeMode.System);
        var changes = 0;
        state.ThemeChanged += () => changes++;

        state.SetSystemPreference(true);
        state.IsDarkMode.ShouldBeTrue();

        state.SetSystemPreference(false);
        state.IsDarkMode.ShouldBeFalse();

        changes.ShouldBe(2);
    }

    [Theory]
    [InlineData(ThemeMode.Light, false)]
    [InlineData(ThemeMode.Dark, true)]
    public void ExplicitMode_IgnoresOsPreference(ThemeMode mode, bool expectedDark)
    {
        var state = CreateState(mode);
        var changes = 0;
        state.ThemeChanged += () => changes++;

        state.SetSystemPreference(!expectedDark);

        state.IsDarkMode.ShouldBe(expectedDark);
        changes.ShouldBe(0);
    }

    [Fact]
    public async Task SetModeAsync_UpdatesSettingsAndPersistsThem()
    {
        var state = CreateState(ThemeMode.System);
        var changes = 0;
        state.ThemeChanged += () => changes++;

        await state.SetModeAsync(ThemeMode.Dark);

        state.Mode.ShouldBe(ThemeMode.Dark);
        state.IsDarkMode.ShouldBeTrue();
        _settings.Theme.ShouldBe(ThemeMode.Dark);
        changes.ShouldBe(1);
        await _settingsProvider.Received(1).NotifySettingsChangedAsync(_settings);
    }

    [Fact]
    public async Task SetModeAsync_SameMode_DoesNothing()
    {
        var state = CreateState(ThemeMode.Light);

        await state.SetModeAsync(ThemeMode.Light);

        await _settingsProvider.DidNotReceive().NotifySettingsChangedAsync(Arg.Any<SettingsBase>());
    }

    [Fact]
    public async Task SwitchingToSystem_UsesLastReportedOsPreference()
    {
        var state = CreateState(ThemeMode.Light);
        state.SetSystemPreference(true);

        await state.SetModeAsync(ThemeMode.System);

        state.IsDarkMode.ShouldBeTrue();
    }

    [Fact]
    public async Task ToggleAsync_FromSystem_ChoosesTheOppositeOfWhatIsShown()
    {
        var state = CreateState(ThemeMode.System);
        state.SetSystemPreference(true);

        await state.ToggleAsync();

        state.Mode.ShouldBe(ThemeMode.Light);
        state.IsDarkMode.ShouldBeFalse();
    }

    [Fact]
    public void ApplySettings_AdoptsLoadedMode()
    {
        var state = CreateState(ThemeMode.System);
        var changes = 0;
        state.ThemeChanged += () => changes++;

        state.ApplySettings(new AppearanceSettings { Theme = ThemeMode.Dark });

        state.Mode.ShouldBe(ThemeMode.Dark);
        state.IsDarkMode.ShouldBeTrue();
        changes.ShouldBe(1);
    }
}
