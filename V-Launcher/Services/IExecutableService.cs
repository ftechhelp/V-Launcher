using System.Windows.Media.Imaging;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service interface for managing executable configurations and icon handling
/// </summary>
public interface IExecutableService
{
    /// <summary>
    /// Retrieves all stored executable configurations
    /// </summary>
    /// <returns>Collection of executable configurations</returns>
    Task<IEnumerable<ExecutableConfiguration>> GetConfigurationsAsync();

    /// <summary>
    /// Saves an executable configuration
    /// </summary>
    /// <param name="config">The executable configuration to save</param>
    /// <param name="oldCustomIconPath">Optional old custom icon path to clear from cache when updating</param>
    /// <returns>The saved executable configuration</returns>
    Task<ExecutableConfiguration> SaveConfigurationAsync(ExecutableConfiguration config, string? oldCustomIconPath = null);

    /// <summary>
    /// Deletes an executable configuration by ID
    /// </summary>
    /// <param name="configId">The ID of the configuration to delete</param>
    Task DeleteConfigurationAsync(Guid configId);

    /// <summary>
    /// Saves the order of executable configurations based on the provided IDs.
    /// </summary>
    /// <param name="orderedConfigurationIds">Configuration IDs in the desired order</param>
    Task SaveConfigurationOrderAsync(IReadOnlyList<Guid> orderedConfigurationIds);

    /// <summary>
    /// Gets the icon for an executable configuration (custom or extracted from executable)
    /// </summary>
    /// <param name="config">The executable configuration</param>
    /// <returns>BitmapImage of the icon, or null if no icon could be loaded</returns>
    Task<BitmapImage?> GetIconAsync(ExecutableConfiguration config);

    /// <summary>
    /// Validates that an executable path exists and is accessible
    /// </summary>
    /// <param name="executablePath">Path to the executable file</param>
    /// <returns>True if the executable is valid and accessible</returns>
    bool ValidateExecutablePath(string executablePath);

    /// <summary>
    /// Extracts the icon from an executable file
    /// </summary>
    /// <param name="executablePath">Path to the executable file</param>
    /// <returns>BitmapImage of the extracted icon, or null if extraction failed</returns>
    Task<BitmapImage?> ExtractExecutableIconAsync(string executablePath);

    /// <summary>
    /// Loads a custom icon from a file path
    /// </summary>
    /// <param name="iconPath">Path to the icon file</param>
    /// <returns>BitmapImage of the custom icon, or null if loading failed</returns>
    Task<BitmapImage?> LoadCustomIconAsync(string iconPath);

    /// <summary>
    /// Clears the icon cache
    /// </summary>
    void ClearIconCache();
}