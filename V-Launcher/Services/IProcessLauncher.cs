using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service interface for launching processes with alternate AD credentials
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Launches an executable with the specified AD account credentials
    /// </summary>
    /// <param name="config">The executable configuration to launch</param>
    /// <param name="account">The AD account to use for authentication</param>
    /// <param name="password">The decrypted password for the AD account</param>
    /// <returns>True if the process was launched successfully, false otherwise</returns>
    Task<bool> LaunchAsync(ExecutableConfiguration config, ADAccount account, string password);

    /// <summary>
    /// Validates that an executable configuration can be launched
    /// </summary>
    /// <param name="config">The executable configuration to validate</param>
    /// <returns>True if the configuration is valid for launching</returns>
    bool ValidateConfiguration(ExecutableConfiguration config);

    /// <summary>
    /// Validates that an AD account has the required information for process launching
    /// </summary>
    /// <param name="account">The AD account to validate</param>
    /// <param name="password">The password to validate</param>
    /// <returns>True if the account credentials are valid for launching</returns>
    bool ValidateCredentials(ADAccount account, string password);
}