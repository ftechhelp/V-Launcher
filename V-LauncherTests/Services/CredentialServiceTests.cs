using System.Security;
using V_Launcher.Models;
using V_Launcher.Services;
using Xunit;

namespace V_Launcher.Tests.Services;

/// <summary>
/// Unit tests for CredentialService focusing on encryption/decryption and DPAPI integration
/// </summary>
public class CredentialServiceTests
{
    private readonly CredentialService _credentialService;
    private readonly TestConfigurationRepository _configurationRepository;

    public CredentialServiceTests()
    {
        _configurationRepository = new TestConfigurationRepository();
        _credentialService = new CredentialService(_configurationRepository);
    }

    [Fact]
    public void EncryptPassword_WithValidPassword_ReturnsEncryptedBytes()
    {
        // Arrange
        const string password = "TestPassword123!";

        // Act
        var encryptedBytes = _credentialService.EncryptPassword(password);

        // Assert
        Assert.NotNull(encryptedBytes);
        Assert.NotEmpty(encryptedBytes);
        Assert.NotEqual(password, Convert.ToBase64String(encryptedBytes));
    }

    [Fact]
    public void EncryptPassword_WithNullPassword_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _credentialService.EncryptPassword(null!));
    }

    [Fact]
    public void EncryptPassword_WithEmptyPassword_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _credentialService.EncryptPassword(string.Empty));
    }

    [Fact]
    public void DecryptPassword_WithValidEncryptedBytes_ReturnsOriginalPassword()
    {
        // Arrange
        const string originalPassword = "TestPassword123!";
        var encryptedBytes = _credentialService.EncryptPassword(originalPassword);

        // Act
        var decryptedPassword = _credentialService.DecryptPassword(encryptedBytes);

        // Assert
        Assert.Equal(originalPassword, decryptedPassword);
    }

    [Fact]
    public void DecryptPassword_WithNullBytes_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _credentialService.DecryptPassword(null!));
    }

    [Fact]
    public void DecryptPassword_WithEmptyBytes_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _credentialService.DecryptPassword(Array.Empty<byte>()));
    }

    [Fact]
    public void EncryptDecrypt_Roundtrip_PreservesPassword()
    {
        // Arrange
        const string originalPassword = "ComplexP@ssw0rd!#$";

        // Act
        var encrypted = _credentialService.EncryptPassword(originalPassword);
        var decrypted = _credentialService.DecryptPassword(encrypted);

        // Assert
        Assert.Equal(originalPassword, decrypted);
    }

    [Fact]
    public async Task SaveAccountAsync_WithValidAccount_EncryptsAndStoresPassword()
    {
        // Arrange
        var account = new ADAccount
        {
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain"
        };
        const string password = "TestPassword123!";

        // Act
        var savedAccount = await _credentialService.SaveAccountAsync(account, password);

        // Assert
        Assert.NotNull(savedAccount.EncryptedPassword);
        Assert.NotEmpty(savedAccount.EncryptedPassword);
        
        // Verify we can decrypt it back
        var decryptedPassword = await _credentialService.DecryptPasswordAsync(savedAccount);
        Assert.Equal(password, decryptedPassword);
    }

    [Fact]
    public async Task SaveAccountAsync_WithNullAccount_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _credentialService.SaveAccountAsync(null!, "password"));
    }

    [Fact]
    public async Task SaveAccountAsync_WithEmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var account = new ADAccount();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _credentialService.SaveAccountAsync(account, string.Empty));
    }

    [Fact]
    public async Task GetAccountsAsync_AfterSavingAccount_ReturnsStoredAccount()
    {
        // Arrange
        var account = new ADAccount
        {
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain"
        };
        await _credentialService.SaveAccountAsync(account, "password");

        // Act
        var accounts = await _credentialService.GetAccountsAsync();

        // Assert
        Assert.Single(accounts);
        Assert.Equal(account.Id, accounts.First().Id);
    }

    [Fact]
    public async Task DeleteAccountAsync_WithExistingAccount_RemovesAccount()
    {
        // Arrange
        var account = new ADAccount
        {
            DisplayName = "Test Account",
            Username = "testuser",
            Domain = "testdomain"
        };
        await _credentialService.SaveAccountAsync(account, "password");

        // Act
        await _credentialService.DeleteAccountAsync(account.Id);

        // Assert
        var accounts = await _credentialService.GetAccountsAsync();
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task DecryptPasswordAsync_WithNullAccount_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _credentialService.DecryptPasswordAsync(null!));
    }

    [Fact]
    public async Task DecryptPasswordAsync_WithAccountWithoutEncryptedPassword_ThrowsInvalidOperationException()
    {
        // Arrange
        var account = new ADAccount();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _credentialService.DecryptPasswordAsync(account));
    }

    [Fact]
    public void EncryptPassword_MultipleCalls_ProducesDifferentResults()
    {
        // Arrange
        const string password = "TestPassword123!";

        // Act
        var encrypted1 = _credentialService.EncryptPassword(password);
        var encrypted2 = _credentialService.EncryptPassword(password);

        // Assert - DPAPI should produce different encrypted results each time
        Assert.NotEqual(encrypted1, encrypted2);
        
        // But both should decrypt to the same password
        Assert.Equal(password, _credentialService.DecryptPassword(encrypted1));
        Assert.Equal(password, _credentialService.DecryptPassword(encrypted2));
    }
}

/// <summary>
/// Test implementation of IConfigurationRepository for unit testing
/// </summary>
public class TestConfigurationRepository : IConfigurationRepository
{
    private readonly ApplicationConfiguration _configuration = new();

    public Task<IEnumerable<ADAccount>> LoadAccountsAsync()
    {
        return Task.FromResult(_configuration.ADAccounts.AsEnumerable());
    }

    public Task SaveAccountsAsync(IEnumerable<ADAccount> accounts)
    {
        _configuration.ADAccounts = accounts.ToList();
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ExecutableConfiguration>> LoadExecutableConfigurationsAsync()
    {
        return Task.FromResult(_configuration.ExecutableConfigurations.AsEnumerable());
    }

    public Task SaveExecutableConfigurationsAsync(IEnumerable<ExecutableConfiguration> configurations)
    {
        _configuration.ExecutableConfigurations = configurations.ToList();
        return Task.CompletedTask;
    }

    public Task<ApplicationConfiguration> LoadConfigurationAsync()
    {
        return Task.FromResult(_configuration);
    }

    public Task SaveConfigurationAsync(ApplicationConfiguration configuration)
    {
        _configuration.ADAccounts = configuration.ADAccounts;
        _configuration.ExecutableConfigurations = configuration.ExecutableConfigurations;
        _configuration.Settings = configuration.Settings;
        _configuration.Version = configuration.Version;
        _configuration.LastSaved = configuration.LastSaved;
        return Task.CompletedTask;
    }

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        return Task.FromResult(_configuration.Settings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        _configuration.Settings = settings;
        return Task.CompletedTask;
    }
}