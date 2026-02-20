using System.Collections.ObjectModel;
using V_Launcher.Models;
using V_Launcher.Resources;
using V_Launcher.Services;
using V_Launcher.ViewModels;
using Xunit;

namespace V_LauncherTests.ViewModels;

/// <summary>
/// Unit tests for AdHocLauncherViewModel.
/// </summary>
public class AdHocLauncherViewModelTests
{
    [Fact]
    public async Task LoadAccountsAsync_ShouldPopulateAvailableAccounts()
    {
        var account = new ADAccount { DisplayName = "Test Account", Username = "user", Domain = "domain" };
        var credentialService = new MockCredentialService(new[] { account });
        var viewModel = CreateViewModel(credentialService, new MockExecutableService(true), new MockProcessLauncher(), new MockClipboardService());

        await viewModel.LoadAccountsCommand.ExecuteAsync(null);

        Assert.Single(viewModel.AvailableAccounts);
        Assert.Equal(account.Id, viewModel.AvailableAccounts[0].Id);
    }

    [Fact]
    public async Task CopyPasswordAsync_WithNoSelectedAccount_ShouldSetError()
    {
        var clipboardService = new MockClipboardService();
        var viewModel = CreateViewModel(new MockCredentialService(Array.Empty<ADAccount>()), new MockExecutableService(true), new MockProcessLauncher(), clipboardService);

        await viewModel.CopyPasswordCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Equal(AdHocResources.AdHocSelectAccountMessage, viewModel.StatusMessage);
        Assert.Null(clipboardService.LastText);
    }

    [Fact]
    public async Task LaunchExecutableAsync_WithValidInput_ShouldLaunchProcess()
    {
        var account = new ADAccount { DisplayName = "Test Account", Username = "user", Domain = "domain" };
        var processLauncher = new MockProcessLauncher();
        var viewModel = CreateViewModel(new MockCredentialService(new[] { account }), new MockExecutableService(true), processLauncher, new MockClipboardService());

        viewModel.SelectedLaunchAccount = account;
        viewModel.ExecutablePath = "C:\\Temp\\app.exe";

        await viewModel.LaunchExecutableCommand.ExecuteAsync(null);

        Assert.True(processLauncher.WasLaunched);
        Assert.Equal(AdHocResources.AdHocLaunchSuccessMessage, viewModel.StatusMessage);
        Assert.False(viewModel.HasError);
    }

    private static AdHocLauncherViewModel CreateViewModel(
        ICredentialService credentialService,
        IExecutableService executableService,
        IProcessLauncher processLauncher,
        IClipboardService clipboardService)
    {
        return new AdHocLauncherViewModel(credentialService, executableService, processLauncher, clipboardService);
    }

    private sealed class MockCredentialService : ICredentialService
    {
        private readonly IReadOnlyCollection<ADAccount> _accounts;

        public MockCredentialService(IEnumerable<ADAccount> accounts)
        {
            _accounts = new ReadOnlyCollection<ADAccount>(accounts.ToList());
        }

        public Task<IEnumerable<ADAccount>> GetAccountsAsync() => Task.FromResult<IEnumerable<ADAccount>>(_accounts);

        public Task<ADAccount> SaveAccountAsync(ADAccount account, string plainPassword) => throw new NotImplementedException();

        public Task DeleteAccountAsync(Guid accountId) => throw new NotImplementedException();

        public Task<string> DecryptPasswordAsync(ADAccount account) => Task.FromResult("TestPassword123!");

        public byte[] EncryptPassword(string plainPassword) => throw new NotImplementedException();

        public string DecryptPassword(byte[] encryptedPassword) => throw new NotImplementedException();
    }

    private sealed class MockExecutableService : IExecutableService
    {
        private readonly bool _isValid;

        public MockExecutableService(bool isValid)
        {
            _isValid = isValid;
        }

        public Task<IEnumerable<ExecutableConfiguration>> GetConfigurationsAsync() => throw new NotImplementedException();

        public Task<ExecutableConfiguration> SaveConfigurationAsync(ExecutableConfiguration config, string? oldCustomIconPath = null) => throw new NotImplementedException();

        public Task DeleteConfigurationAsync(Guid configId) => throw new NotImplementedException();

        public Task SaveConfigurationOrderAsync(IReadOnlyList<Guid> orderedConfigurationIds) => throw new NotImplementedException();

        public Task<System.Windows.Media.Imaging.BitmapImage?> GetIconAsync(ExecutableConfiguration config) => throw new NotImplementedException();

        public bool ValidateExecutablePath(string executablePath) => _isValid;

        public Task<System.Windows.Media.Imaging.BitmapImage?> ExtractExecutableIconAsync(string executablePath) => throw new NotImplementedException();

        public Task<System.Windows.Media.Imaging.BitmapImage?> LoadCustomIconAsync(string iconPath) => throw new NotImplementedException();

        public void ClearIconCache() => throw new NotImplementedException();
    }

    private sealed class MockProcessLauncher : IProcessLauncher
    {
        public bool WasLaunched { get; private set; }

        public Task<bool> LaunchAsync(ExecutableConfiguration config, ADAccount account, string password)
        {
            WasLaunched = true;
            return Task.FromResult(true);
        }

        public bool ValidateConfiguration(ExecutableConfiguration config) => true;

        public bool ValidateCredentials(ADAccount account, string password) => true;
    }

    private sealed class MockClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text)
        {
            LastText = text;
        }
    }
}
