using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// JSON-based configuration repository for persistent storage
/// </summary>
public class ConfigurationRepository : IConfigurationRepository
{
    private const string IntegrityFormatVersion = "2.0";
    private const string IntegrityAlgorithm = "HMACSHA256";

    private readonly string _configurationFilePath;
    private readonly IReadOnlyList<string> _backupFilePaths;
    private readonly string _integrityKeyFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the ConfigurationRepository
    /// </summary>
    public ConfigurationRepository()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "V-Launcher");
        var localAppFolder = Path.Combine(localAppDataPath, "V-Launcher");
        
        // Ensure the application folder exists
        Directory.CreateDirectory(appFolder);
        Directory.CreateDirectory(localAppFolder);
        
        _configurationFilePath = Path.Combine(appFolder, "configuration.json");
        _integrityKeyFilePath = Path.Combine(appFolder, "configuration.integrity.key");
        _backupFilePaths = new[]
        {
            Path.Combine(appFolder, "configuration.backup.json"),
            Path.Combine(localAppFolder, "configuration.backup.json")
        };
        
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
        _integrityKeyFilePath = _configurationFilePath + ".integrity.key";
        _backupFilePaths = new[] { _configurationFilePath + ".bak" };
        
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
            var readResults = new List<ConfigurationReadResult>();
            var primaryReadResult = await TryReadConfigurationFileAsync(_configurationFilePath);
            readResults.Add(primaryReadResult);
            if (primaryReadResult.Configuration is not null)
            {
                return primaryReadResult.Configuration;
            }

            var recoveredConfiguration = await RecoverConfigurationFromBackupsAsync(readResults);
            if (recoveredConfiguration is not null)
            {
                return recoveredConfiguration;
            }

            var repairedConfiguration = await TryRepairFromMatchingSignedCopiesAsync(readResults);
            if (repairedConfiguration is not null)
            {
                return repairedConfiguration;
            }

            if (primaryReadResult.Failure is JsonException jsonException)
            {
                throw new InvalidOperationException($"Failed to deserialize configuration file: {jsonException.Message}", jsonException);
            }

            if (primaryReadResult.Failure is InvalidDataException invalidDataException)
            {
                throw new InvalidOperationException($"Failed to validate configuration file integrity: {invalidDataException.Message}", invalidDataException);
            }

            return new ApplicationConfiguration();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to read configuration file: {ex.Message}", ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException($"Failed to validate configuration file integrity: {ex.Message}", ex);
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
            await PersistConfigurationAsync(configuration);
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

    private async Task<ApplicationConfiguration?> RecoverConfigurationFromBackupsAsync(ICollection<ConfigurationReadResult> readResults)
    {
        foreach (var backupFilePath in _backupFilePaths)
        {
            var backupReadResult = await TryReadConfigurationFileAsync(backupFilePath);
            readResults.Add(backupReadResult);
            if (backupReadResult.Configuration is null || string.IsNullOrWhiteSpace(backupReadResult.StoredContent))
            {
                continue;
            }

            await WriteConfigurationAtomicallyAsync(_configurationFilePath, backupReadResult.StoredContent);

            return backupReadResult.Configuration;
        }

        return null;
    }

    private async Task<ApplicationConfiguration?> TryRepairFromMatchingSignedCopiesAsync(IEnumerable<ConfigurationReadResult> readResults)
    {
        var signedResults = readResults
            .Where(result => result.IsSignedEnvelope && !string.IsNullOrWhiteSpace(result.NormalizedConfigurationJson))
            .ToList();

        if (signedResults.Count < 2)
        {
            return null;
        }

        if (signedResults.Any(result => result.RecoverableConfiguration is null))
        {
            return null;
        }

        var normalizedPayload = signedResults[0].NormalizedConfigurationJson!;
        if (signedResults.Any(result => !string.Equals(result.NormalizedConfigurationJson, normalizedPayload, StringComparison.Ordinal)))
        {
            return null;
        }

        var recoveredConfiguration = signedResults[0].RecoverableConfiguration!;
        await PersistConfigurationAsync(recoveredConfiguration);
        return recoveredConfiguration;
    }

    private async Task<ConfigurationReadResult> TryReadConfigurationFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ConfigurationReadResult.Empty;
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return ConfigurationReadResult.Empty;
            }

            using var jsonDocument = JsonDocument.Parse(jsonContent);
            if (IsSignedEnvelope(jsonDocument.RootElement))
            {
                var normalizedConfigurationJson = NormalizeConfigurationPayload(
                    jsonDocument.RootElement.GetProperty("configuration").GetRawText());
                var envelope = JsonSerializer.Deserialize<ConfigurationEnvelope>(jsonContent, _jsonOptions);
                if (envelope?.Configuration is null)
                {
                    throw new JsonException("Signed configuration envelope did not contain configuration data.");
                }

                try
                {
                    await ValidateIntegrityAsync(envelope, normalizedConfigurationJson);
                    return new ConfigurationReadResult(envelope.Configuration, jsonContent, null, envelope.Configuration, normalizedConfigurationJson, true);
                }
                catch (InvalidDataException ex)
                {
                    return new ConfigurationReadResult(null, jsonContent, ex, envelope.Configuration, normalizedConfigurationJson, true);
                }
                catch (CryptographicException ex)
                {
                    return new ConfigurationReadResult(null, jsonContent, ex, envelope.Configuration, normalizedConfigurationJson, true);
                }
            }

            var configuration = JsonSerializer.Deserialize<ApplicationConfiguration>(jsonContent, _jsonOptions);
            return new ConfigurationReadResult(configuration ?? new ApplicationConfiguration(), jsonContent, null, null, null, false);
        }
        catch (JsonException ex)
        {
            return new ConfigurationReadResult(null, null, ex, null, null, false);
        }
    }

    private async Task PersistConfigurationAsync(ApplicationConfiguration configuration)
    {
        configuration.LastSaved = DateTime.UtcNow;

        var configurationJson = JsonSerializer.Serialize(configuration, _jsonOptions);
        var normalizedConfigurationJson = NormalizeConfigurationPayload(configurationJson);
        var signature = await ComputeIntegritySignatureAsync(normalizedConfigurationJson);
        var envelope = new ConfigurationEnvelope
        {
            FormatVersion = IntegrityFormatVersion,
            Configuration = configuration,
            Integrity = new ConfigurationIntegrityMetadata
            {
                Algorithm = IntegrityAlgorithm,
                Signature = signature
            }
        };

        var jsonContent = JsonSerializer.Serialize(envelope, _jsonOptions);

        await WriteConfigurationAtomicallyAsync(_configurationFilePath, jsonContent);

        foreach (var backupFilePath in _backupFilePaths)
        {
            await WriteConfigurationAtomicallyAsync(backupFilePath, jsonContent);
        }
    }

    private static bool IsSignedEnvelope(JsonElement rootElement)
    {
        return rootElement.ValueKind == JsonValueKind.Object
            && rootElement.TryGetProperty("configuration", out _)
            && rootElement.TryGetProperty("integrity", out _);
    }

    private async Task ValidateIntegrityAsync(ConfigurationEnvelope envelope, string normalizedConfigurationJson)
    {
        if (envelope.Integrity is null)
        {
            throw new InvalidDataException("Configuration integrity metadata is missing.");
        }

        if (!string.Equals(envelope.Integrity.Algorithm, IntegrityAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Configuration integrity algorithm is not supported.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Integrity.Signature))
        {
            throw new InvalidDataException("Configuration integrity signature is missing.");
        }

        byte[] expectedSignature;
        byte[] providedSignature;

        try
        {
            expectedSignature = await ComputeIntegritySignatureBytesAsync(normalizedConfigurationJson, createKeyIfMissing: false);
            providedSignature = Convert.FromHexString(envelope.Integrity.Signature);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Configuration integrity signature format is invalid.", ex);
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            throw new InvalidDataException("Configuration integrity validation failed.");
        }
    }

    private async Task<string> ComputeIntegritySignatureAsync(string normalizedConfigurationJson)
    {
        var signatureBytes = await ComputeIntegritySignatureBytesAsync(normalizedConfigurationJson, createKeyIfMissing: true);
        return Convert.ToHexString(signatureBytes);
    }

    private async Task<byte[]> ComputeIntegritySignatureBytesAsync(string normalizedConfigurationJson, bool createKeyIfMissing)
    {
        var key = await GetIntegrityKeyAsync(createKeyIfMissing);

        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedConfigurationJson));
    }

    private string NormalizeConfigurationPayload(string configurationJson)
    {
        using var jsonDocument = JsonDocument.Parse(configurationJson);
        return JsonSerializer.Serialize(jsonDocument.RootElement, _jsonOptions);
    }

    private async Task<byte[]> GetIntegrityKeyAsync(bool createKeyIfMissing)
    {
        if (!File.Exists(_integrityKeyFilePath))
        {
            if (!createKeyIfMissing)
            {
                throw new InvalidDataException("Configuration integrity key is missing.");
            }

            var newKey = RandomNumberGenerator.GetBytes(32);
            var protectedKey = ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser);
            await WriteBytesAtomicallyAsync(_integrityKeyFilePath, protectedKey);
            return newKey;
        }

        var encryptedKey = await File.ReadAllBytesAsync(_integrityKeyFilePath);
        if (encryptedKey.Length == 0)
        {
            throw new InvalidDataException("Configuration integrity key is empty.");
        }

        return ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
    }

    private static async Task WriteConfigurationAtomicallyAsync(string targetPath, string content)
    {
        var tempFilePath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempFilePath, content);

        if (File.Exists(targetPath))
        {
            File.Replace(tempFilePath, targetPath, null);
        }
        else
        {
            File.Move(tempFilePath, targetPath);
        }
    }

    private static async Task WriteBytesAtomicallyAsync(string targetPath, byte[] content)
    {
        var tempFilePath = targetPath + ".tmp";
        await File.WriteAllBytesAsync(tempFilePath, content);

        if (File.Exists(targetPath))
        {
            File.Replace(tempFilePath, targetPath, null);
        }
        else
        {
            File.Move(tempFilePath, targetPath);
        }
    }

    /// <summary>
    /// Disposes the repository and releases resources
    /// </summary>
    public void Dispose()
    {
        _fileLock?.Dispose();
    }

    private sealed record ConfigurationReadResult(
        ApplicationConfiguration? Configuration,
        string? StoredContent,
        Exception? Failure,
        ApplicationConfiguration? RecoverableConfiguration,
        string? NormalizedConfigurationJson,
        bool IsSignedEnvelope)
    {
        public static ConfigurationReadResult Empty { get; } = new(null, null, null, null, null, false);
    }

    private sealed class ConfigurationEnvelope
    {
        public string FormatVersion { get; set; } = IntegrityFormatVersion;

        public ApplicationConfiguration? Configuration { get; set; }

        public ConfigurationIntegrityMetadata? Integrity { get; set; }
    }

    private sealed class ConfigurationIntegrityMetadata
    {
        public string Algorithm { get; set; } = IntegrityAlgorithm;

        public string Signature { get; set; } = string.Empty;
    }
}