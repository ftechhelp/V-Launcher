using System.IO;
using System.Text.Json;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// JSON-based configuration repository for persistent storage
/// </summary>
public class ConfigurationRepository : IConfigurationRepository
{
    private readonly string _configurationFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the ConfigurationRepository
    /// </summary>
    public ConfigurationRepository()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "V-Launcher");
        
        // Ensure the application folder exists
        Directory.CreateDirectory(appFolder);
        
        _configurationFilePath = Path.Combine(appFolder, "configuration.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Initializes a new instance with a custom configuration file path (for testing)
    /// </summary>
    /// <param name="configurationFilePath">Custom path for the configuration file</param>
    public ConfigurationRepository(string configurationFilePath)
    {
        _configurationFilePath = configurationFilePath;
        
        // Ensure the directory exists
        var directory = Path.GetDirectoryName(_configurationFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IEnumerable<ADAccount>> LoadAccountsAsync()
    {
        var configuration = await LoadConfigurationAsync();
        return configuration.ADAccounts;
    }

    public async Task SaveAccountsAsync(IEnumerable<ADAccount> accounts)
    {
        var configuration = await LoadConfigurationAsync();
        configuration.ADAccounts = accounts.ToList();
        configuration.LastSaved = DateTime.UtcNow;
        await SaveConfigurationAsync(configuration);
    }

    public async Task<IEnumerable<ExecutableConfiguration>> LoadExecutableConfigurationsAsync()
    {
        var configuration = await LoadConfigurationAsync();
        return configuration.ExecutableConfigurations;
    }

    public async Task SaveExecutableConfigurationsAsync(IEnumerable<ExecutableConfiguration> configurations)
    {
        var configuration = await LoadConfigurationAsync();
        configuration.ExecutableConfigurations = configurations.ToList();
        configuration.LastSaved = DateTime.UtcNow;
        await SaveConfigurationAsync(configuration);
    }

    public async Task<IEnumerable<NetworkDriveConfiguration>> LoadNetworkDriveConfigurationsAsync()
    {
        var configuration = await LoadConfigurationAsync();
        return configuration.NetworkDriveConfigurations;
    }

    public async Task SaveNetworkDriveConfigurationsAsync(IEnumerable<NetworkDriveConfiguration> configurations)
    {
        var configuration = await LoadConfigurationAsync();
        configuration.NetworkDriveConfigurations = configurations.ToList();
        configuration.LastSaved = DateTime.UtcNow;
        await SaveConfigurationAsync(configuration);
    }

    public async Task<ApplicationConfiguration> LoadConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_configurationFilePath))
            {
                return new ApplicationConfiguration();
            }

            var jsonContent = await File.ReadAllTextAsync(_configurationFilePath);
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new ApplicationConfiguration();
            }

            var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(jsonContent, _jsonOptions);
            return configuration ?? new ApplicationConfiguration();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize configuration file: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to read configuration file: {ex.Message}", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveConfigurationAsync(ApplicationConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        await _fileLock.WaitAsync();
        try
        {
            configuration.LastSaved = DateTime.UtcNow;
            
            var jsonContent = JsonSerializer.Serialize(configuration, _jsonOptions);
            
            // Write to a temporary file first, then move to prevent corruption
            var tempFilePath = _configurationFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFilePath, jsonContent);
            
            // Atomic move operation
            if (File.Exists(_configurationFilePath))
            {
                File.Replace(tempFilePath, _configurationFilePath, null);
            }
            else
            {
                File.Move(tempFilePath, _configurationFilePath);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to serialize configuration: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to write configuration file: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException($"Failed to write configuration file: {ex.Message}", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<ApplicationSettings> LoadSettingsAsync()
    {
        var configuration = await LoadConfigurationAsync();
        return configuration.Settings;
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var configuration = await LoadConfigurationAsync();
        configuration.Settings = settings;
        configuration.LastSaved = DateTime.UtcNow;
        await SaveConfigurationAsync(configuration);
    }

    /// <summary>
    /// Gets the path to the configuration file
    /// </summary>
    public string ConfigurationFilePath => _configurationFilePath;

    /// <summary>
    /// Disposes the repository and releases resources
    /// </summary>
    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}