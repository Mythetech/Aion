using System.Reflection;
using Hermes.Diagnostics;
using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.Infrastructure.Smoke;
using Mythetech.Platform.Sdk.Diagnostics;

namespace Aion.Desktop.Services;

/// <summary>
/// Turns crash reporting on only when the user consented to it. A smoke run never reports, whatever the consent,
/// so CI runs and local smoke runs stay out of production diagnostics.
/// </summary>
public class CrashReportingHook : IAsyncInitializationHook
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ICrashReportingService _crashReporter;
    private readonly ISmokeTestContext _smokeTest;

    public CrashReportingHook(
        ISettingsProvider settingsProvider,
        ICrashReportingService crashReporter,
        ISmokeTestContext smokeTest)
    {
        _settingsProvider = settingsProvider;
        _crashReporter = crashReporter;
        _smokeTest = smokeTest;
    }

    public int Order => 200;

    public string Name => "CrashReporting";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var privacySettings = _settingsProvider.GetSettings<PrivacySettings>();

        if (!_smokeTest.IsEnabled && privacySettings?.CrashReportingEnabled == true)
        {
            HermesCrashInterceptor.ProductName = "Aion";
            HermesCrashInterceptor.ProductVersion = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            HermesCrashInterceptor.OnCrash = ctx => _crashReporter.ReportCrash(ctx);
            HermesCrashInterceptor.Enable();
        }

        return Task.CompletedTask;
    }
}
