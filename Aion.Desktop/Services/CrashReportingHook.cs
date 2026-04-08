using System.Reflection;
using Hermes.Diagnostics;
using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Platform.Sdk.Diagnostics;

namespace Aion.Desktop.Services;

public class CrashReportingHook : IAsyncInitializationHook
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ICrashReportingService _crashReporter;

    public CrashReportingHook(
        ISettingsProvider settingsProvider,
        ICrashReportingService crashReporter)
    {
        _settingsProvider = settingsProvider;
        _crashReporter = crashReporter;
    }

    public int Order => 200;

    public string Name => "CrashReporting";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var privacySettings = _settingsProvider.GetSettings<PrivacySettings>();

        if (privacySettings?.CrashReportingEnabled == true)
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
