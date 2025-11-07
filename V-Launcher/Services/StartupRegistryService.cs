using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace V_Launcher.Services;

/// <summary>
/// Service for managing Windows startup registry entries
/// </summary>
public class StartupRegistryService : IStartupRegistryService
{
    private const string STARTUP_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APPLICATION_NAME = "V-Launcher";

    /// <summary>
    /// Enables the application to start automatically with Windows
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> EnableStartupAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true);
                if (key == null)
                {
                    return false;
                }

                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    return false;
                }

                // Set the registry value to the executable path
                key.SetValue(APPLICATION_NAME, executablePath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Registry access denied
                return false;
            }
            catch (Exception)
            {
                // Other registry-related errors
                return false;
            }
        });
    }

    /// <summary>
    /// Disables the application from starting automatically with Windows
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> DisableStartupAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, true);
                if (key == null)
                {
                    return false;
                }

                // Check if the value exists before trying to delete it
                if (key.GetValue(APPLICATION_NAME) != null)
                {
                    key.DeleteValue(APPLICATION_NAME);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Registry access denied
                return false;
            }
            catch (ArgumentException)
            {
                // Value doesn't exist - consider this success
                return true;
            }
            catch (Exception)
            {
                // Other registry-related errors
                return false;
            }
        });
    }

    /// <summary>
    /// Checks if the application is currently set to start with Windows
    /// </summary>
    /// <returns>True if startup is enabled, false otherwise</returns>
    public async Task<bool> IsStartupEnabledAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(STARTUP_REGISTRY_KEY, false);
                if (key == null)
                {
                    return false;
                }

                var value = key.GetValue(APPLICATION_NAME) as string;
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                // Verify that the registry entry points to the current executable
                var currentPath = GetExecutablePath();
                return string.Equals(value, currentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Any error checking registry - assume not enabled
                return false;
            }
        });
    }

    /// <summary>
    /// Sets the startup state based on the provided flag
    /// </summary>
    /// <param name="enabled">True to enable startup, false to disable</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SetStartupEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            return await EnableStartupAsync();
        }
        else
        {
            return await DisableStartupAsync();
        }
    }

    /// <summary>
    /// Gets the current executable path
    /// </summary>
    /// <returns>The full path to the current executable, or null if not available</returns>
    private static string? GetExecutablePath()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var location = assembly.Location;

            // For single-file deployments, use the process path instead
            if (string.IsNullOrEmpty(location) || location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var processPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
                {
                    return processPath;
                }
            }

            // For regular deployments, use the assembly location
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return location;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}