using Mythetech.Framework.Desktop.Updates;
using Mythetech.Framework.Infrastructure.Initialization;
using Mythetech.Framework.Infrastructure.Smoke;

namespace Aion.Desktop.Updates;

/// <summary>
/// Runs the framework's startup update check once persisted settings are loaded, so the
/// "Check for updates on startup" setting is honored.
/// </summary>
/// <remarks>
/// The initialization pipeline only marks the point where settings are ready. The check itself is
/// awaited by <see cref="UpdateBannerHost"/> after it renders, because the app awaits every
/// initialization hook before loading connections, and Velopack's feed request can take as long as
/// its HTTP timeout on a stalled network.
///
/// A smoke run skips the check entirely: it would reach the update feed, could download an update under the
/// auto-download setting, and a failed request logs an error, which fails the run for a network blip.
/// </remarks>
public class StartupUpdateCheck : IAsyncInitializationHook
{
    private readonly IUpdateService _updateService;
    private readonly ISmokeTestContext _smokeTest;
    private readonly TaskCompletionSource _settingsLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public StartupUpdateCheck(IUpdateService updateService, ISmokeTestContext smokeTest)
    {
        _updateService = updateService;
        _smokeTest = smokeTest;
    }

    public int Order => 900;

    public string Name => "StartupUpdateCheck";

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settingsLoaded.TrySetResult();
        return Task.CompletedTask;
    }

    public async Task RunAsync()
    {
        if (_smokeTest.IsEnabled)
            return;

        await _settingsLoaded.Task;
        await _updateService.CheckForUpdatesOnStartupAsync();
    }
}
