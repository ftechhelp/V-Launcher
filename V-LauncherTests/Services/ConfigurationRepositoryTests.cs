using System.IO;
using System.Text.Json;
using V_Launcher.Models;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

public class ConfigurationRepositoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testConfigPath;
    private readonly string _testBackupPath;
    private readonly ConfigurationRepository _repository;

    public ConfigurationRepositoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "V-LauncherTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "test-config.json");
        _testBackupPath = _testConfigPath + ".bak";
        _repository = new ConfigurationRepository(_testConfigPath);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenFileDoesNotExist_ReturnsEmptyConfiguration()
    {
        // Act
        var result = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.ADAccounts);
        Assert.Empty(result.ExecutableConfigurations);
        Assert.Equal("1.0", result.Version);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfiguration_CreatesFile()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts = new List<ADAccount>
            {
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Test Account",
                    Username = "testuser",
                    Domain = "testdomain",
                    EncryptedPassword = new byte[] { 1, 2, 3, 4 }
                }
            }
        };

        // Act
        await _repository.SaveConfigurationAsync(configuration);

        // Assert
        Assert.True(File.Exists(_testConfigPath));
        var fileContent = await File.ReadAllTextAsync(_testConfigPath);
        Assert.Contains("testuser", fileContent);
        Assert.Contains("testdomain", fileContent);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfiguration_CreatesBackupFile()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts = new List<ADAccount>
            {
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Backup Test Account",
                    Username = "backupuser",
                    Domain = "backupdomain"
                }
            }
        };

        // Act
        await _repository.SaveConfigurationAsync(configuration);

        // Assert
        Assert.True(File.Exists(_testBackupPath));
        var backupContent = await File.ReadAllTextAsync(_testBackupPath);
        Assert.Contains("backupuser", backupContent);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfiguration_WritesIntegrityEnvelope()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Integrity Account",
                    Username = "integrityuser",
                    Domain = "integritydomain"
                }
            ]
        };

        // Act
        await _repository.SaveConfigurationAsync(configuration);
        var fileContent = await File.ReadAllTextAsync(_testConfigPath);

        // Assert
        Assert.Contains("\"configuration\"", fileContent);
        Assert.Contains("\"integrity\"", fileContent);
    }

    [Fact]
    public async Task LoadConfigurationAsync_AfterSaving_ReturnsCorrectData()
    {
        // Arrange
        var originalAccount = new ADAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain",
            EncryptedPassword = new byte[] { 1, 2, 3, 4 }
        };

        var originalConfig = new ExecutableConfiguration
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test App",
            ExecutablePath = @"C:\test\app.exe",
            ADAccountId = originalAccount.Id,
            Arguments = "--test"
        };

        var configuration = new ApplicationConfiguration
        {
            ADAccounts = new List<ADAccount> { originalAccount },
            ExecutableConfigurations = new List<ExecutableConfiguration> { originalConfig }
        };

        // Act
        await _repository.SaveConfigurationAsync(configuration);
        var loadedConfiguration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.Single(loadedConfiguration.ADAccounts);
        Assert.Single(loadedConfiguration.ExecutableConfigurations);

        var loadedAccount = loadedConfiguration.ADAccounts.First();
        Assert.Equal(originalAccount.Id, loadedAccount.Id);
        Assert.Equal(originalAccount.DisplayName, loadedAccount.DisplayName);
        Assert.Equal(originalAccount.Username, loadedAccount.Username);
        Assert.Equal(originalAccount.Domain, loadedAccount.Domain);
        Assert.Equal(originalAccount.EncryptedPassword, loadedAccount.EncryptedPassword);

        var loadedConfig = loadedConfiguration.ExecutableConfigurations.First();
        Assert.Equal(originalConfig.Id, loadedConfig.Id);
        Assert.Equal(originalConfig.DisplayName, loadedConfig.DisplayName);
        Assert.Equal(originalConfig.ExecutablePath, loadedConfig.ExecutablePath);
        Assert.Equal(originalConfig.ADAccountId, loadedConfig.ADAccountId);
        Assert.Equal(originalConfig.Arguments, loadedConfig.Arguments);
    }

    [Fact]
    public async Task LoadAccountsAsync_ReturnsAccountsFromConfiguration()
    {
        // Arrange
        var account = new ADAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain"
        };

        var configuration = new ApplicationConfiguration
        {
            ADAccounts = new List<ADAccount> { account }
        };

        await _repository.SaveConfigurationAsync(configuration);

        // Act
        var accounts = await _repository.LoadAccountsAsync();

        // Assert
        Assert.Single(accounts);
        Assert.Equal(account.Id, accounts.First().Id);
    }

    [Fact]
    public async Task SaveAccountsAsync_UpdatesConfigurationWithAccounts()
    {
        // Arrange
        var account = new ADAccount
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act
        await _repository.SaveAccountsAsync(new[] { account });

        // Assert
        var configuration = await _repository.LoadConfigurationAsync();
        Assert.Single(configuration.ADAccounts);
        Assert.Equal(account.Id, configuration.ADAccounts.First().Id);
    }

    [Fact]
    public async Task LoadExecutableConfigurationsAsync_ReturnsConfigurationsFromStorage()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test App",
            ExecutablePath = @"C:\test\app.exe",
            ADAccountId = Guid.NewGuid()
        };

        var configuration = new ApplicationConfiguration
        {
            ExecutableConfigurations = new List<ExecutableConfiguration> { config }
        };

        await _repository.SaveConfigurationAsync(configuration);

        // Act
        var configurations = await _repository.LoadExecutableConfigurationsAsync();

        // Assert
        Assert.Single(configurations);
        Assert.Equal(config.Id, configurations.First().Id);
    }

    [Fact]
    public async Task SaveExecutableConfigurationsAsync_UpdatesConfigurationWithConfigurations()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test App",
            ExecutablePath = @"C:\test\app.exe",
            ADAccountId = Guid.NewGuid()
        };

        // Act
        await _repository.SaveExecutableConfigurationsAsync(new[] { config });

        // Assert
        var configuration = await _repository.LoadConfigurationAsync();
        Assert.Single(configuration.ExecutableConfigurations);
        Assert.Equal(config.Id, configuration.ExecutableConfigurations.First().Id);
    }

    [Fact]
    public async Task SaveNetworkDriveConfigurationsAsync_UpdatesConfigurationWithNetworkDrives()
    {
        // Arrange
        var configuration = new NetworkDriveConfiguration
        {
            Id = Guid.NewGuid(),
            DisplayName = "Fileshare",
            RemotePath = "\\\\server\\share",
            ADAccountId = Guid.NewGuid()
        };

        // Act
        await _repository.SaveNetworkDriveConfigurationsAsync(new[] { configuration });

        // Assert
        var loaded = await _repository.LoadConfigurationAsync();
        Assert.Single(loaded.NetworkDriveConfigurations);
        Assert.Equal(configuration.RemotePath, loaded.NetworkDriveConfigurations[0].RemotePath);
    }

    [Fact]
    public async Task LoadNetworkDriveConfigurationsAsync_ReturnsConfigurationsFromStorage()
    {
        // Arrange
        var driveConfig = new NetworkDriveConfiguration
        {
            Id = Guid.NewGuid(),
            DisplayName = "Finance",
            RemotePath = "\\\\server\\finance",
            ADAccountId = Guid.NewGuid()
        };

        await _repository.SaveConfigurationAsync(new ApplicationConfiguration
        {
            NetworkDriveConfigurations = new List<NetworkDriveConfiguration> { driveConfig }
        });

        // Act
        var loaded = await _repository.LoadNetworkDriveConfigurationsAsync();

        // Assert
        Assert.Single(loaded);
        Assert.Equal(driveConfig.DisplayName, loaded.First().DisplayName);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveConfigurationAsync(null!));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithCorruptedFile_ThrowsInvalidOperationException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testConfigPath, "invalid json content");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.LoadConfigurationAsync());
        Assert.Contains("Failed to deserialize configuration file", exception.Message);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenPrimaryFileDeleted_RecoversFromBackup()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Recovery Account",
                    Username = "recoveruser",
                    Domain = "recoverdomain"
                }
            ]
        };

        await _repository.SaveConfigurationAsync(configuration);
        File.Delete(_testConfigPath);

        // Act
        var recoveredConfiguration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.Single(recoveredConfiguration.ADAccounts);
        Assert.Equal("recoveruser", recoveredConfiguration.ADAccounts[0].Username);
        Assert.True(File.Exists(_testConfigPath));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenPrimaryFileCorrupted_RecoversFromBackup()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Corruption Recovery Account",
                    Username = "restoreuser",
                    Domain = "restoredomain"
                }
            ]
        };

        await _repository.SaveConfigurationAsync(configuration);
        await File.WriteAllTextAsync(_testConfigPath, "invalid json content");

        // Act
        var recoveredConfiguration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.Single(recoveredConfiguration.ADAccounts);
        Assert.Equal("restoreuser", recoveredConfiguration.ADAccounts[0].Username);

        var primaryContent = await File.ReadAllTextAsync(_testConfigPath);
        Assert.Contains("restoreuser", primaryContent);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenPrimarySignedConfigurationIsTampered_RecoversFromBackup()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Tamper Recovery Account",
                    Username = "secureuser",
                    Domain = "securedomain"
                }
            ]
        };

        await _repository.SaveConfigurationAsync(configuration);

        var tamperedPrimaryContent = await File.ReadAllTextAsync(_testConfigPath);
        tamperedPrimaryContent = tamperedPrimaryContent.Replace("secureuser", "tampereduser", StringComparison.Ordinal);
        await File.WriteAllTextAsync(_testConfigPath, tamperedPrimaryContent);

        // Act
        var recoveredConfiguration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.Equal("secureuser", recoveredConfiguration.ADAccounts[0].Username);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenSignedConfigurationAndBackupAreTampered_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Tamper Detection Account",
                    Username = "signeduser",
                    Domain = "signeddomain"
                }
            ]
        };

        await _repository.SaveConfigurationAsync(configuration);

        var tamperedPrimaryContent = await File.ReadAllTextAsync(_testConfigPath);
        tamperedPrimaryContent = tamperedPrimaryContent.Replace("signeduser", "tamperedprimary", StringComparison.Ordinal);
        await File.WriteAllTextAsync(_testConfigPath, tamperedPrimaryContent);

        var tamperedBackupContent = await File.ReadAllTextAsync(_testBackupPath);
        tamperedBackupContent = tamperedBackupContent.Replace("signeduser", "tamperedbackup", StringComparison.Ordinal);
        await File.WriteAllTextAsync(_testBackupPath, tamperedBackupContent);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.LoadConfigurationAsync());
        Assert.Contains("Failed to validate configuration file integrity", exception.Message);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithLegacyPlainConfiguration_LoadsSuccessfully()
    {
        // Arrange
        var legacyConfiguration = new ApplicationConfiguration
        {
            ADAccounts =
            [
                new ADAccount
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Legacy Account",
                    Username = "legacyuser",
                    Domain = "legacydomain"
                }
            ]
        };

        var legacyJson = JsonSerializer.Serialize(legacyConfiguration, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(_testConfigPath, legacyJson);

        // Act
        var loadedConfiguration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.Equal("legacyuser", loadedConfiguration.ADAccounts[0].Username);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithReadOnlyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        await File.WriteAllTextAsync(_testConfigPath, "{}");
        File.SetAttributes(_testConfigPath, FileAttributes.ReadOnly);

        var configuration = new ApplicationConfiguration();

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.SaveConfigurationAsync(configuration));
            Assert.Contains("Failed to write configuration file", exception.Message);
        }
        finally
        {
            // Cleanup
            File.SetAttributes(_testConfigPath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        // Act
        var settings = await _repository.LoadSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.False(settings.StartOnWindowsStart);
        Assert.False(settings.StartMinimized);
        Assert.False(settings.MinimizeOnClose);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithValidSettings_PersistsCorrectly()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = true,
            MinimizeOnClose = false
        };

        // Act
        await _repository.SaveSettingsAsync(settings);

        // Assert
        var loadedSettings = await _repository.LoadSettingsAsync();
        Assert.False(loadedSettings.StartOnWindowsStart);
        Assert.True(loadedSettings.StartMinimized);
        Assert.False(loadedSettings.MinimizeOnClose);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveSettingsAsync(null!));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithSettings_IncludesSettingsInConfiguration()
    {
        // Arrange
        var settings = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = false,
            MinimizeOnClose = true
        };

        await _repository.SaveSettingsAsync(settings);

        // Act
        var configuration = await _repository.LoadConfigurationAsync();

        // Assert
        Assert.NotNull(configuration.Settings);
        Assert.False(configuration.Settings.StartOnWindowsStart);
        Assert.False(configuration.Settings.StartMinimized);
        Assert.True(configuration.Settings.MinimizeOnClose);
    }

    public void Dispose()
    {
        _repository?.Dispose();
        
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                // Remove read-only attributes if any
                foreach (var file in Directory.GetFiles(_testDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}