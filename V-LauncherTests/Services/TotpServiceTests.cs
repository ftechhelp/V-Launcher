using Microsoft.Extensions.Logging;
using OtpNet;
using V_Launcher.Models;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

/// <summary>
/// Unit tests for TotpService covering key generation, code validation, and enable/disable flows
/// </summary>
public class TotpServiceTests
{
    private readonly TotpService _totpService;
    private readonly TotpTestConfigurationRepository _configurationRepository;

    public TotpServiceTests()
    {
        _configurationRepository = new TotpTestConfigurationRepository();
        var logger = new LoggerFactory().CreateLogger<TotpService>();
        _totpService = new TotpService(_configurationRepository, logger);
    }

    [Fact]
    public void GenerateSecretKey_ReturnsNonEmptyBase32String()
    {
        // Act
        string secretKey = _totpService.GenerateSecretKey();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(secretKey));
        // Verify it's valid Base32 by decoding without exception
        byte[] decoded = Base32Encoding.ToBytes(secretKey);
        Assert.Equal(20, decoded.Length);
    }

    [Fact]
    public void GenerateSecretKey_ReturnsDifferentKeysEachCall()
    {
        // Act
        string key1 = _totpService.GenerateSecretKey();
        string key2 = _totpService.GenerateSecretKey();

        // Assert
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateOtpAuthUri_ReturnsValidUri()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();

        // Act
        string uri = _totpService.GenerateOtpAuthUri(secretKey);

        // Assert
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=" + secretKey, uri);
        Assert.Contains("issuer=V-Launcher", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }

    [Fact]
    public void GenerateOtpAuthUri_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _totpService.GenerateOtpAuthUri(null!));
    }

    [Fact]
    public void ValidateCode_WithCorrectCode_ReturnsTrue()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();
        byte[] secretBytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(secretBytes, step: 30, totpSize: 6);
        string currentCode = totp.ComputeTotp();

        // Act
        bool result = _totpService.ValidateCode(currentCode, secretKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateCode_WithIncorrectCode_ReturnsFalse()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();

        // Act
        bool result = _totpService.ValidateCode("000000", secretKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCode_WithEmptyCode_ReturnsFalse()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();

        // Act
        bool result = _totpService.ValidateCode("", secretKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCode_WithNullCode_ReturnsFalse()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();

        // Act
        bool result = _totpService.ValidateCode(null!, secretKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCode_WithNullSecretKey_ReturnsFalse()
    {
        // Act
        bool result = _totpService.ValidateCode("123456", null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnableOtpAsync_StoresEncryptedSecretAndSetsEnabled()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();

        // Act
        await _totpService.EnableOtpAsync(secretKey);

        // Assert
        Assert.True(_configurationRepository.Configuration.IsOtpEnabled);
        Assert.NotNull(_configurationRepository.Configuration.OtpEncryptedSecret);
        Assert.NotEmpty(_configurationRepository.Configuration.OtpEncryptedSecret);
    }

    [Fact]
    public async Task EnableOtpAsync_WithNullKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _totpService.EnableOtpAsync(null!));
    }

    [Fact]
    public async Task EnableOtpAsync_WithEmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _totpService.EnableOtpAsync(""));
    }

    [Fact]
    public async Task ResetOtpAsync_AfterEnable_ClearsStoredSecretAndDisablesOtp()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();
        await _totpService.EnableOtpAsync(secretKey);

        // Act
        await _totpService.ResetOtpAsync();

        // Assert
        Assert.False(_configurationRepository.Configuration.IsOtpEnabled);
        Assert.Null(_configurationRepository.Configuration.OtpEncryptedSecret);
    }

    [Fact]
    public async Task ResetOtpAsync_AfterEnable_SetsIsOtpConfiguredFalse()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();
        await _totpService.EnableOtpAsync(secretKey);
        await _totpService.LoadConfigurationAsync();

        // Act
        await _totpService.ResetOtpAsync();

        // Assert
        Assert.False(_totpService.IsOtpConfigured);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenOtpEnabled_SetsIsOtpConfiguredTrue()
    {
        // Arrange - enable OTP first
        string secretKey = _totpService.GenerateSecretKey();
        await _totpService.EnableOtpAsync(secretKey);

        // Create a new service instance to test loading
        var newService = new TotpService(_configurationRepository, new LoggerFactory().CreateLogger<TotpService>());

        // Act
        await newService.LoadConfigurationAsync();

        // Assert
        Assert.True(newService.IsOtpConfigured);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WhenOtpDisabled_SetsIsOtpConfiguredFalse()
    {
        // Create a new service with clean config
        var newService = new TotpService(_configurationRepository, new LoggerFactory().CreateLogger<TotpService>());

        // Act
        await newService.LoadConfigurationAsync();

        // Assert
        Assert.False(newService.IsOtpConfigured);
    }

    [Fact]
    public async Task ValidateCode_WithStoredSecret_ValidatesCorrectly()
    {
        // Arrange
        string secretKey = _totpService.GenerateSecretKey();
        await _totpService.EnableOtpAsync(secretKey);
        await _totpService.LoadConfigurationAsync();

        byte[] secretBytes = Base32Encoding.ToBytes(secretKey);
        var totp = new Totp(secretBytes, step: 30, totpSize: 6);
        string currentCode = totp.ComputeTotp();

        // Act - validate using stored secret (no explicit key parameter)
        bool result = _totpService.ValidateCode(currentCode);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsOtpConfigured_WhenNotLoaded_ReturnsFalse()
    {
        // Assert - before LoadConfigurationAsync is called
        Assert.False(_totpService.IsOtpConfigured);
    }

    /// <summary>
    /// Test implementation of IConfigurationRepository for TotpService tests
    /// </summary>
    private class TotpTestConfigurationRepository : IConfigurationRepository
    {
        public ApplicationConfiguration Configuration { get; } = new();

        public Task<IEnumerable<ADAccount>> LoadAccountsAsync()
            => Task.FromResult(Configuration.ADAccounts.AsEnumerable());

        public Task SaveAccountsAsync(IEnumerable<ADAccount> accounts)
        {
            Configuration.ADAccounts = accounts.ToList();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<ExecutableConfiguration>> LoadExecutableConfigurationsAsync()
            => Task.FromResult(Configuration.ExecutableConfigurations.AsEnumerable());

        public Task SaveExecutableConfigurationsAsync(IEnumerable<ExecutableConfiguration> configurations)
        {
            Configuration.ExecutableConfigurations = configurations.ToList();
            return Task.CompletedTask;
        }

        public Task<IEnumerable<NetworkDriveConfiguration>> LoadNetworkDriveConfigurationsAsync()
            => Task.FromResult(Configuration.NetworkDriveConfigurations.AsEnumerable());

        public Task SaveNetworkDriveConfigurationsAsync(IEnumerable<NetworkDriveConfiguration> configurations)
        {
            Configuration.NetworkDriveConfigurations = configurations.ToList();
            return Task.CompletedTask;
        }

        public Task<ApplicationConfiguration> LoadConfigurationAsync()
            => Task.FromResult(Configuration);

        public Task SaveConfigurationAsync(ApplicationConfiguration configuration)
        {
            Configuration.ADAccounts = configuration.ADAccounts;
            Configuration.ExecutableConfigurations = configuration.ExecutableConfigurations;
            Configuration.NetworkDriveConfigurations = configuration.NetworkDriveConfigurations;
            Configuration.Settings = configuration.Settings;
            Configuration.IsOtpEnabled = configuration.IsOtpEnabled;
            Configuration.OtpEncryptedSecret = configuration.OtpEncryptedSecret;
            Configuration.Version = configuration.Version;
            Configuration.LastSaved = configuration.LastSaved;
            return Task.CompletedTask;
        }

        public Task<ApplicationSettings> LoadSettingsAsync()
            => Task.FromResult(Configuration.Settings);

        public Task SaveSettingsAsync(ApplicationSettings settings)
        {
            Configuration.Settings = settings;
            return Task.CompletedTask;
        }
    }
}
