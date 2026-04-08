using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Platform.Sdk.Diagnostics;

namespace Aion.Desktop.Services;

public class ErrorReportingHook : IAsyncInitializationHook
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ErrorReportingOptions _options;

    public ErrorReportingHook(
        ISettingsProvider settingsProvider,
        ErrorReportingOptions options)
    {
        _settingsProvider = settingsProvider;
        _options = options;
    }

    public int Order => 200;

    public string Name => "ErrorReporting";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var privacySettings = _settingsProvider.GetSettings<PrivacySettings>();

        if (privacySettings?.ErrorReportingEnabled == true)
        {
            _options.Enabled = true;
        }

        return Task.CompletedTask;
    }
}
