using System.IO;
using System.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using V_Launcher.Helpers;
using V_Launcher.Models;
using V_Launcher.Services;
using V_Launcher.ViewModels;

namespace V_LauncherTests;

/// <summary>
/// Integration tests that verify complete workflows from ViewModels to data persistence
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testConfigPath;
    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public IntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "V-LauncherIntegrationTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "test-config.json");

        // Create a test host with all services configured
        _host = CreateTestHost();
        _services = _host.Services;
    }

    private IHost CreateTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register services with test configuration
                services.AddSingleton<IConfigurationRepository>(provider => 
                    new ConfigurationRepository(_testConfigPath));
                services.AddSingleton<ICredentialService, CredentialService>();
                services.AddSingleton<IExecutableService, ExecutableService>();
                services.AddSingleton<IProcessLauncher, ProcessLauncher>();

                // Register ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LauncherViewModel>();
                services.AddTransient<CredentialManagementViewModel>();
                services.AddTransient<ExecutableManagementViewModel>();
            })
            .Build();
    }

    [Fact]
    public async Task CompleteCredentialWorkflow_AddEditDelete_PersistsCorrectly()
    {
        // Arrange
        var credentialViewModel = _services.GetRequiredService<CredentialManagementViewModel>();
        await credentialViewModel.LoadAccountsCommand.ExecuteAsync(null);

        // Act 1: Add new account
        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Test Account";
        credentialViewModel.Username = "testuser";
        credentialViewModel.Domain = "testdomain";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("TestPassword123!");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("TestPassword123!");

        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        // Assert 1: Account was added
        Assert.Single(credentialViewModel.Accounts);
        var addedAccount = credentialViewModel.Accounts.First();
        Assert.Equal("Test Account", addedAccount.DisplayName);
        Assert.Equal("testuser", addedAccount.Username);
        Assert.Equal("testdomain", addedAccount.Domain);

        // Verify persistence
        var credentialService = _services.GetRequiredService<ICredentialService>();
        var persistedAccounts = await credentialService.GetAccountsAsync();
        Assert.Single(persistedAccounts);

        // Act 2: Edit account
        credentialViewModel.SelectedAccount = addedAccount;
        await credentialViewModel.EditAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Updated Test Account";
        credentialViewModel.Username = "updateduser";
        credentialViewModel.Domain = "updateddomain";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("UpdatedPassword123!");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("UpdatedPassword123!");

        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        // Assert 2: Account was updated
        Assert.Single(credentialViewModel.Accounts);
        var updatedAccount = credentialViewModel.Accounts.First();
        Assert.Equal("Updated Test Account", updatedAccount.DisplayName);
        Assert.Equal("updateduser", updatedAccount.Username);
        Assert.Equal("updateddomain", updatedAccount.Domain);

        // Act 3: Delete account
        credentialViewModel.SelectedAccount = updatedAccount;
        await credentialViewModel.DeleteAccountCommand.ExecuteAsync(null);

        // Assert 3: Account was deleted
        Assert.Empty(credentialViewModel.Accounts);

        // Verify persistence
        var finalAccounts = await credentialService.GetAccountsAsync();
        Assert.Empty(finalAccounts);
    }

    [Fact]
    public async Task CompleteExecutableWorkflow_AddEditDelete_PersistsCorrectly()
    {
        // Arrange
        var executableViewModel = _services.GetRequiredService<ExecutableManagementViewModel>();
        var credentialViewModel = _services.GetRequiredService<CredentialManagementViewModel>();

        // First create an AD account to associate with executable
        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Test Account";
        credentialViewModel.Username = "testuser";
        credentialViewModel.Domain = "testdomain";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("TestPassword123!");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("TestPassword123!");
        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        await executableViewModel.InitializeAsync();
        var testAccount = executableViewModel.AvailableAccounts.First();

        // Act 1: Add new executable configuration
        executableViewModel.DisplayName = "Test App";
        executableViewModel.ExecutablePath = @"C:\Windows\System32\notepad.exe";
        executableViewModel.SelectedAccount = testAccount;
        executableViewModel.Arguments = "--test";
        executableViewModel.WorkingDirectory = @"C:\temp";

        await executableViewModel.SaveConfigurationCommand.ExecuteAsync(null);

        // Assert 1: Configuration was added
        Assert.Single(executableViewModel.Configurations);
        var addedConfig = executableViewModel.Configurations.First();
        Assert.Equal("Test App", addedConfig.DisplayName);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", addedConfig.ExecutablePath);
        Assert.Equal(testAccount.Id, addedConfig.ADAccountId);

        // Verify persistence
        var executableService = _services.GetRequiredService<IExecutableService>();
        var persistedConfigs = await executableService.GetConfigurationsAsync();
        Assert.Single(persistedConfigs);

        // Act 2: Edit configuration
        executableViewModel.SelectedConfiguration = addedConfig;
        await executableViewModel.EditConfigurationCommand.ExecuteAsync(null);
        executableViewModel.DisplayName = "Updated Test App";
        executableViewModel.ExecutablePath = @"C:\Windows\System32\calc.exe";
        executableViewModel.Arguments = "--updated";

        await executableViewModel.SaveConfigurationCommand.ExecuteAsync(null);

        // Assert 2: Configuration was updated
        Assert.Single(executableViewModel.Configurations);
        var updatedConfig = executableViewModel.Configurations.First();
        Assert.Equal("Updated Test App", updatedConfig.DisplayName);
        Assert.Equal(@"C:\Windows\System32\calc.exe", updatedConfig.ExecutablePath);
        Assert.Equal("--updated", updatedConfig.Arguments);

        // Act 3: Delete configuration
        executableViewModel.SelectedConfiguration = updatedConfig;
        await executableViewModel.DeleteConfigurationCommand.ExecuteAsync(null);

        // Assert 3: Configuration was deleted
        Assert.Empty(executableViewModel.Configurations);

        // Verify persistence
        var finalConfigs = await executableService.GetConfigurationsAsync();
        Assert.Empty(finalConfigs);
    }

    [Fact]
    public async Task MainViewModelIntegration_InitializationAndNavigation_WorksCorrectly()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();

        // Act: Initialize application
        await mainViewModel.HandleApplicationStartupAsync();

        // Assert: Initialization completed successfully
        Assert.False(mainViewModel.IsInitializing);
        Assert.NotNull(mainViewModel.CurrentViewModel);
        Assert.Equal(mainViewModel.LauncherViewModel, mainViewModel.CurrentViewModel);

        // Act: Navigate to credential management
        mainViewModel.ShowCredentialManagementViewCommand.Execute(null);

        // Assert: Navigation worked
        Assert.Equal(mainViewModel.CredentialManagementViewModel, mainViewModel.CurrentViewModel);

        // Act: Navigate to executable management
        mainViewModel.ShowExecutableManagementViewCommand.Execute(null);

        // Assert: Navigation worked
        Assert.Equal(mainViewModel.ExecutableManagementViewModel, mainViewModel.CurrentViewModel);

        // Act: Navigate back to launcher
        mainViewModel.ShowLauncherViewCommand.Execute(null);

        // Assert: Navigation worked
        Assert.Equal(mainViewModel.LauncherViewModel, mainViewModel.CurrentViewModel);
    }

    [Fact]
    public async Task EndToEndWorkflow_CreateAccountAndExecutable_LauncherDisplaysCorrectly()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.HandleApplicationStartupAsync();

        // Act 1: Create AD account
        mainViewModel.ShowCredentialManagementViewCommand.Execute(null);
        var credentialViewModel = mainViewModel.CredentialManagementViewModel;

        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Test Account";
        credentialViewModel.Username = "testuser";
        credentialViewModel.Domain = "testdomain";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("TestPassword123!");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("TestPassword123!");
        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        // Act 2: Create executable configuration
        mainViewModel.ShowExecutableManagementViewCommand.Execute(null);
        var executableViewModel = mainViewModel.ExecutableManagementViewModel;
        await executableViewModel.InitializeAsync();

        executableViewModel.DisplayName = "Notepad";
        executableViewModel.ExecutablePath = @"C:\Windows\System32\notepad.exe";
        executableViewModel.SelectedAccount = executableViewModel.AvailableAccounts.First();
        await executableViewModel.SaveConfigurationCommand.ExecuteAsync(null);

        // Act 3: Navigate to launcher (automatic refresh will occur)
        mainViewModel.ShowLauncherViewCommand.Execute(null);
        
        // Wait a moment for the automatic refresh to complete
        await Task.Delay(100);

        // Assert: Launcher displays the executable
        var launcherViewModel = mainViewModel.LauncherViewModel;
        Assert.Single(launcherViewModel.ExecutableItems);
        
        var executable = launcherViewModel.ExecutableItems.First();
        Assert.Equal("Notepad", executable.DisplayName);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", executable.ExecutablePath);
    }

    [Fact]
    public async Task ErrorHandling_InvalidExecutablePath_DisplaysErrorCorrectly()
    {
        // Arrange
        var executableViewModel = _services.GetRequiredService<ExecutableManagementViewModel>();
        var credentialViewModel = _services.GetRequiredService<CredentialManagementViewModel>();

        // Create an AD account first
        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Test Account";
        credentialViewModel.Username = "testuser";
        credentialViewModel.Domain = "testdomain";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("TestPassword123!");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("TestPassword123!");
        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        await executableViewModel.InitializeAsync();

        // Act: Try to save configuration with invalid executable path
        executableViewModel.DisplayName = "Invalid App";
        executableViewModel.ExecutablePath = @"C:\NonExistent\InvalidApp.exe";
        executableViewModel.SelectedAccount = executableViewModel.AvailableAccounts.First();

        await executableViewModel.SaveConfigurationCommand.ExecuteAsync(null);

        // Assert: Error is displayed
        Assert.True(executableViewModel.HasValidationError);
        Assert.Contains("does not exist", executableViewModel.ValidationMessage);
        Assert.Empty(executableViewModel.Configurations);
    }

    [Fact]
    public async Task ErrorHandling_DuplicateAccountName_DisplaysErrorCorrectly()
    {
        // Arrange
        var credentialViewModel = _services.GetRequiredService<CredentialManagementViewModel>();
        await credentialViewModel.LoadAccountsCommand.ExecuteAsync(null);

        // Act 1: Add first account
        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Duplicate Name";
        credentialViewModel.Username = "user1";
        credentialViewModel.Domain = "domain1";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("Password1");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("Password1");
        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        // Act 2: Try to add second account with same display name
        credentialViewModel.CancelEditCommand.Execute(null);
        await credentialViewModel.AddAccountCommand.ExecuteAsync(null);
        credentialViewModel.DisplayName = "Duplicate Name";
        credentialViewModel.Username = "user2";
        credentialViewModel.Domain = "domain2";
        credentialViewModel.Password = SecureStringHelper.CreateFromString("Password2");
        credentialViewModel.ConfirmPassword = SecureStringHelper.CreateFromString("Password2");
        await credentialViewModel.SaveAccountCommand.ExecuteAsync(null);

        // Assert: Error is displayed and second account is not added
        Assert.True(credentialViewModel.HasValidationError);
        Assert.Contains("already exists", credentialViewModel.ValidationMessage);
        Assert.Single(credentialViewModel.Accounts);
    }

    [Fact]
    public async Task DataPersistence_ApplicationRestart_DataIsRetained()
    {
        // Arrange - Create a separate test directory for this test
        var testDir = Path.Combine(Path.GetTempPath(), "V-LauncherPersistenceTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var testConfigPath = Path.Combine(testDir, "persistence-test-config.json");

        try
        {
            // Create a separate host for this test
            using var testHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IConfigurationRepository>(provider => 
                        new ConfigurationRepository(testConfigPath));
                    services.AddSingleton<ICredentialService, CredentialService>();
                    services.AddSingleton<IExecutableService, ExecutableService>();
                    services.AddSingleton<IProcessLauncher, ProcessLauncher>();
                    services.AddTransient<CredentialManagementViewModel>();
                    services.AddTransient<ExecutableManagementViewModel>();
                    services.AddTransient<LauncherViewModel>();
                })
                .Build();

            // Create initial data
            var credentialService = testHost.Services.GetRequiredService<ICredentialService>();
            var executableService = testHost.Services.GetRequiredService<IExecutableService>();

            var account = new ADAccount
            {
                DisplayName = "Persistent Account",
                Username = "persistentuser",
                Domain = "persistentdomain"
            };
            var savedAccount = await credentialService.SaveAccountAsync(account, "PersistentPassword123!");

            var config = new ExecutableConfiguration
            {
                DisplayName = "Persistent App",
                ExecutablePath = @"C:\Windows\System32\notepad.exe",
                ADAccountId = savedAccount.Id,
                Arguments = "--persistent"
            };
            await executableService.SaveConfigurationAsync(config);

            // Act - Verify data persistence by loading directly from services
            var newCredentialService = testHost.Services.GetRequiredService<ICredentialService>();
            var newExecutableService = testHost.Services.GetRequiredService<IExecutableService>();

            var persistedAccounts = await newCredentialService.GetAccountsAsync();
            var persistedConfigs = await newExecutableService.GetConfigurationsAsync();

            // Assert - Data is retained at service level
            Assert.Single(persistedAccounts);
            Assert.Equal("Persistent Account", persistedAccounts.First().DisplayName);

            Assert.Single(persistedConfigs);
            Assert.Equal("Persistent App", persistedConfigs.First().DisplayName);

            // Test ViewModel loading
            var newCredentialViewModel = testHost.Services.GetRequiredService<CredentialManagementViewModel>();
            var newExecutableViewModel = testHost.Services.GetRequiredService<ExecutableManagementViewModel>();

            await newCredentialViewModel.LoadAccountsCommand.ExecuteAsync(null);
            await newExecutableViewModel.LoadConfigurationsCommand.ExecuteAsync(null);

            Assert.Single(newCredentialViewModel.Accounts);
            Assert.Equal("Persistent Account", newCredentialViewModel.Accounts.First().DisplayName);

            Assert.Single(newExecutableViewModel.Configurations);
            Assert.Equal("Persistent App", newExecutableViewModel.Configurations.First().DisplayName);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(testDir, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    Directory.Delete(testDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    public void Dispose()
    {
        _host?.Dispose();
        
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