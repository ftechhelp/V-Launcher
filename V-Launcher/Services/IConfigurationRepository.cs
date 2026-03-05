using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Interface for configuration data persistence operations
/// </summary>
public interface IConfigurationRepository
{
    /// <summary>
    /// Loads all AD accounts from persistent storage
    /// </summary>
    /// <returns>Collection of AD accounts</returns>
    Task<IEnumerable<ADAccount>> LoadAccountsAsync();

    /// <summary>
    /// Saves AD accounts to persistent storage
    /// </summary>
    /// <param name="accounts">Collection of AD accounts to save</param>
    Task SaveAccountsAsync(IEnumerable<ADAccount> accounts);

    /// <summary>
    /// Loads all executable configurations from persistent storage
    /// </summary>
    /// <returns>Collection of executable configurations</returns>
    Task<IEnumerable<ExecutableConfiguration>> LoadExecutableConfigurationsAsync();

    /// <summary>
    /// Saves executable configurations to persistent storage
    /// </summary>
    /// <param name="configurations">Collection of executable configurations to save</param>
    Task SaveExecutableConfigurationsAsync(IEnumerable<ExecutableConfiguration> configurations);

    /// <summary>
    /// Loads all network drive configurations from persistent storage.
    /// </summary>
    /// <returns>Collection of network drive configurations</returns>
    Task<IEnumerable<NetworkDriveConfiguration>> LoadNetworkDriveConfigurationsAsync();

    /// <summary>
    /// Saves network drive configurations to persistent storage.
    /// </summary>
    /// <param name="configurations">Collection of network drive configurations to save</param>
    Task SaveNetworkDriveConfigurationsAsync(IEnumerable<NetworkDriveConfiguration> configurations);

    /// <summary>
    /// Loads the complete application configuration
    /// </summary>
    /// <returns>Application configuration containing all data</returns>
    Task<ApplicationConfiguration> LoadConfigurationAsync();

    /// <summary>
    /// Saves the complete application configuration
    /// </summary>
    /// <param name="configuration">Application configuration to save</param>
    Task SaveConfigurationAsync(ApplicationConfiguration configuration);

    /// <summary>
    /// Loads application settings from persistent storage
    /// </summary>
    /// <returns>Application settings</returns>
    Task<ApplicationSettings> LoadSettingsAsync();

    /// <summary>
    /// Saves application settings to persistent storage
    /// </summary>
    /// <param name="settings">Application settings to save</param>
    Task SaveSettingsAsync(ApplicationSettings settings);
}