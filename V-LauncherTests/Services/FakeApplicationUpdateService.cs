using V_Launcher.Services;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

public sealed class FakeApplicationUpdateService : IApplicationUpdateService
{
    public Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateCheckResult(
            IsUpdateAvailable: false,
            CurrentVersion: new Version(1, 0, 0, 0),
            LatestVersion: null,
            LatestTag: null,
            InstallerUrl: null,
            InstallerSha256: null,
            InstallerChecksumUrl: null));
    }

    public Task<bool> InstallUpdateAsync(UpdateCheckResult updateCheckResult, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
