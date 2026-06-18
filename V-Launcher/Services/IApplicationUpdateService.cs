namespace V_Launcher.Services;

/// <summary>
/// Provides update discovery and installation operations for the application.
/// </summary>
public interface IApplicationUpdateService
{
    /// <summary>
    /// Checks the remote source for a newer application version.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update check details.</returns>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts installation of an available update.
    /// </summary>
    /// <param name="updateCheckResult">The update check result that contains installer details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the install attempt, including a failure reason when it did not start.</returns>
    Task<UpdateInstallResult> InstallUpdateAsync(UpdateCheckResult updateCheckResult, CancellationToken cancellationToken = default);
}
