using System;
using System.Threading.Tasks;

namespace V_Launcher.Services;

/// <summary>
/// Service for managing Windows startup registry entries
/// </summary>
public interface IStartupRegistryService
{
    /// <summary>
    /// Enables the application to start automatically with Windows
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> EnableStartupAsync();

    /// <summary>
    /// Disables the application from starting automatically with Windows
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DisableStartupAsync();

    /// <summary>
    /// Checks if the application is currently set to start with Windows
    /// </summary>
    /// <returns>True if startup is enabled, false otherwise</returns>
    Task<bool> IsStartupEnabledAsync();

    /// <summary>
    /// Sets the startup state based on the provided flag
    /// </summary>
    /// <param name="enabled">True to enable startup, false to disable</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetStartupEnabledAsync(bool enabled);
}