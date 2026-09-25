using Aion.Components.Settings.Domains;
using Mythetech.Framework.Infrastructure.MessageBus;
using Mythetech.Framework.Infrastructure.Settings.Events;

namespace Aion.Components.Theme.Consumers;

/// <summary>
/// Hands appearance settings to <see cref="ThemeState"/> when they are loaded from storage
/// or saved from the settings dialog.
/// </summary>
public class AppearanceSettingsApplier : IConsumer<SettingsModelChanged<AppearanceSettings>>
{
    private readonly ThemeState _themeState;

    public AppearanceSettingsApplier(ThemeState themeState)
    {
        _themeState = themeState;
    }

    public Task Consume(SettingsModelChanged<AppearanceSettings> message)
    {
        _themeState.ApplySettings(message.Settings);
        return Task.CompletedTask;
    }
}
