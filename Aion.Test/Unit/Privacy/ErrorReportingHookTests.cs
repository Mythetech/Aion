using Aion.Desktop.Services;
using Mythetech.Framework.Infrastructure.Privacy;
using Mythetech.Framework.Infrastructure.Settings;
using Mythetech.Framework.Infrastructure.Smoke;
using Mythetech.Platform.Sdk.Diagnostics;
using NSubstitute;
using Shouldly;

namespace Aion.Test.Unit.Privacy;

public class ErrorReportingHookTests
{
    private readonly ErrorReportingOptions _options = new();
    private readonly ISettingsProvider _settings = Substitute.For<ISettingsProvider>();
    private readonly ISmokeTestContext _smokeTest = Substitute.For<ISmokeTestContext>();
    private readonly ErrorReportingHook _hook;

    public ErrorReportingHookTests()
    {
        _hook = new ErrorReportingHook(_settings, _options, _smokeTest);
    }

    [Fact]
    public async Task Consent_turns_error_reporting_on()
    {
        _settings.GetSettings<PrivacySettings>().Returns(new PrivacySettings { ErrorReportingEnabled = true });

        await _hook.InitializeAsync();

        _options.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Without_consent_error_reporting_is_off_although_the_sdk_defaults_to_on()
    {
        _options.Enabled = true;
        _settings.GetSettings<PrivacySettings>().Returns(new PrivacySettings { ErrorReportingEnabled = false });

        await _hook.InitializeAsync();

        _options.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task A_smoke_run_reports_nothing_whatever_the_consent()
    {
        _smokeTest.IsEnabled.Returns(true);
        _settings.GetSettings<PrivacySettings>().Returns(new PrivacySettings { ErrorReportingEnabled = true });

        await _hook.InitializeAsync();

        _options.Enabled.ShouldBeFalse();
    }
}
