using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.Infrastructure.Smoke;
using Mythetech.Platform.Sdk.Diagnostics;

namespace Aion.Desktop.Services;

/// <summary>
/// Turns error reporting on only when the user consented to it. A smoke run never reports, whatever the consent,
/// so CI runs and local smoke runs stay out of production diagnostics.
/// </summary>
public class ErrorReportingHook : IAsyncInitializationHook
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ErrorReportingOptions _options;
    private readonly ISmokeTestContext _smokeTest;

    public ErrorReportingHook(
        ISettingsProvider settingsProvider,
        ErrorReportingOptions options,
        ISmokeTestContext smokeTest)
    {
        _settingsProvider = settingsProvider;
        _options = options;
        _smokeTest = smokeTest;
    }

    public int Order => 200;

    public string Name => "ErrorReporting";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var privacySettings = _settingsProvider.GetSettings<PrivacySettings>();

        // Assigned both ways because the SDK's default is enabled, so leaving it untouched would report
        // without consent.
        _options.Enabled = !_smokeTest.IsEnabled && privacySettings?.ErrorReportingEnabled == true;

        return Task.CompletedTask;
    }
}
