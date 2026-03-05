using Microsoft.Extensions.Logging;
using V_Launcher.Models;
using V_Launcher.Services;
using V_Launcher.ViewModels;
using Xunit;

namespace V_LauncherTests.ViewModels;

/// <summary>
/// Unit tests for SettingsViewModel
/// </summary>
public class SettingsViewModelTests : IDisposable
{
    private readonly MockConfigurationRepository _mockRepository;
    private readonly MockStartupRegistryService _mockRegistryService;
    private readonly MockLogger<SettingsViewModel> _mockLogger;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _mockRepository = new MockConfigurationRepository();
        _mockRegistryService = new MockStartupRegistryService();
        _mockLogger = new MockLogger<SettingsViewModel>();
        
        _viewModel = new SettingsViewModel(_mockRepository, _mockRegistryService, _mockLogger);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultSettings()
    {
        // Assert
        Assert.NotNull(_viewModel.Settings);
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Equal(LauncherOrderMode.Custom, _viewModel.Settings.LauncherOrderMode);
        Assert.False(_viewModel.IsLoading);
        Assert.False(_viewModel.IsSaving);
        Assert.Empty(_viewModel.StatusMessage);
        Assert.False(_viewModel.HasError);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsViewModel(null!, _mockRegistryService, _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullRegistryService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsViewModel(_mockRepository, null!, _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsViewModel(_mockRepository, _mockRegistryService, null!));
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldLoadSettingsFromRepository()
    {
        // Arrange
        var expectedSettings = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = true,
            MinimizeOnClose = false,
            LauncherOrderMode = LauncherOrderMode.Alphabetical
        };
        _mockRepository.SetSettings(expectedSettings);

        // Act
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(expectedSettings.StartOnWindowsStart, _viewModel.Settings.StartOnWindowsStart);
        Assert.Equal(expectedSettings.StartMinimized, _viewModel.Settings.StartMinimized);
        Assert.Equal(expectedSettings.MinimizeOnClose, _viewModel.Settings.MinimizeOnClose);
        Assert.Equal(expectedSettings.LauncherOrderMode, _viewModel.Settings.LauncherOrderMode);
        Assert.False(_viewModel.IsLoading);
        Assert.Contains("Settings loaded successfully", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadSettingsAsync_WithRepositoryError_ShouldUseDefaultSettings()
    {
        // Arrange
        _mockRepository.ShouldThrowOnLoad = true;

        // Act
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Equal(LauncherOrderMode.Custom, _viewModel.Settings.LauncherOrderMode);
        Assert.True(_viewModel.HasError);
        Assert.Contains("Failed to load settings", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldSaveSettingsToRepository()
    {
        // Arrange
        _viewModel.Settings.StartOnWindowsStart = false;
        _viewModel.Settings.StartMinimized = false;
        _viewModel.Settings.LauncherOrderMode = LauncherOrderMode.Custom;

        // Act
        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        // Assert
        var savedSettings = _mockRepository.GetSavedSettings();
        Assert.NotNull(savedSettings);
        Assert.False(savedSettings.StartOnWindowsStart);
        Assert.False(savedSettings.StartMinimized);
        Assert.Equal(LauncherOrderMode.Custom, savedSettings.LauncherOrderMode);
        Assert.Contains("Settings saved successfully", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithRepositoryError_ShouldShowError()
    {
        // Arrange
        _mockRepository.ShouldThrowOnSave = true;

        // Act
        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        // Assert
        Assert.True(_viewModel.HasError);
        Assert.Contains("Failed to save settings", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleStartOnWindowsStartAsync_WithTrue_ShouldEnableStartupAndSaveSettings()
    {
        // Act
        await _viewModel.ToggleStartOnWindowsStartCommand.ExecuteAsync(true);

        // Assert
        Assert.True(_viewModel.Settings.StartOnWindowsStart);
        Assert.True(_mockRegistryService.IsStartupEnabled);
        Assert.Contains("Windows startup enabled successfully", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleStartOnWindowsStartAsync_WithFalse_ShouldDisableStartupAndSaveSettings()
    {
        // Act
        await _viewModel.ToggleStartOnWindowsStartCommand.ExecuteAsync(false);

        // Assert
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_mockRegistryService.IsStartupEnabled);
        Assert.Contains("Windows startup disabled successfully", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ToggleStartOnWindowsStartAsync_WithRegistryError_ShouldShowError()
    {
        // Arrange
        _mockRegistryService.ShouldFailOnSet = true;

        // Act
        await _viewModel.ToggleStartOnWindowsStartCommand.ExecuteAsync(true);

        // Assert
        Assert.True(_viewModel.HasError);
        Assert.Contains("Failed to update Windows startup setting", _viewModel.StatusMessage);
    }

    [Fact]
    public void ToggleStartMinimized_ShouldUpdateSettingAndSave()
    {
        // Act
        _viewModel.ToggleStartMinimizedCommand.Execute(false);

        // Assert
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.Contains("Start minimized disabled", _viewModel.StatusMessage);
    }

    [Fact]
    public void ToggleMinimizeOnClose_ShouldUpdateSettingAndSave()
    {
        // Act
        _viewModel.ToggleMinimizeOnCloseCommand.Execute(false);

        // Assert
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Contains("Minimize on close disabled", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldResetAllSettingsToDefaults()
    {
        // Arrange
        _viewModel.Settings.StartOnWindowsStart = true;
        _viewModel.Settings.StartMinimized = true;
        _viewModel.Settings.MinimizeOnClose = true;
        _viewModel.Settings.LauncherOrderMode = LauncherOrderMode.Alphabetical;

        // Act
        await _viewModel.ResetToDefaultsCommand.ExecuteAsync(null);

        // Assert
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Equal(LauncherOrderMode.Custom, _viewModel.Settings.LauncherOrderMode);
        Assert.False(_mockRegistryService.IsStartupEnabled);
        Assert.Contains("Settings reset to defaults", _viewModel.StatusMessage);
    }

    [Fact]
    public void GetCurrentSettings_ShouldReturnCopyOfSettings()
    {
        // Arrange
        _viewModel.Settings.StartOnWindowsStart = false;
        _viewModel.Settings.LauncherOrderMode = LauncherOrderMode.Custom;

        // Act
        var currentSettings = _viewModel.GetCurrentSettings();

        // Assert
        Assert.NotSame(_viewModel.Settings, currentSettings);
        Assert.Equal(_viewModel.Settings.StartOnWindowsStart, currentSettings.StartOnWindowsStart);
        Assert.Equal(_viewModel.Settings.LauncherOrderMode, currentSettings.LauncherOrderMode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettingsAndSave()
    {
        // Arrange
        var newSettings = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = false,
            MinimizeOnClose = false,
            LauncherOrderMode = LauncherOrderMode.Alphabetical
        };

        // Act
        await _viewModel.UpdateSettingsAsync(newSettings);

        // Assert
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Equal(LauncherOrderMode.Alphabetical, _viewModel.Settings.LauncherOrderMode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithNull_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _viewModel.UpdateSettingsAsync(null!));
    }

    [Fact]
    public async Task RefreshSettingsAsync_ShouldReloadSettings()
    {
        // Arrange
        var newSettings = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = false,
            MinimizeOnClose = false,
            LauncherOrderMode = LauncherOrderMode.Alphabetical
        };
        _mockRepository.SetSettings(newSettings);

        // Act
        await _viewModel.RefreshSettingsAsync();

        // Assert
        Assert.False(_viewModel.Settings.StartOnWindowsStart);
        Assert.False(_viewModel.Settings.StartMinimized);
        Assert.False(_viewModel.Settings.MinimizeOnClose);
        Assert.Equal(LauncherOrderMode.Alphabetical, _viewModel.Settings.LauncherOrderMode);
    }

    [Fact]
    public void CommandCanExecute_WhenNotLoadingOrSaving_ShouldReturnTrue()
    {
        // Assert
        Assert.True(_viewModel.SaveSettingsCommand.CanExecute(null));
        Assert.True(_viewModel.ResetToDefaultsCommand.CanExecute(null));
        Assert.True(_viewModel.ToggleStartOnWindowsStartCommand.CanExecute(true));
        Assert.True(_viewModel.ToggleStartMinimizedCommand.CanExecute(true));
        Assert.True(_viewModel.ToggleMinimizeOnCloseCommand.CanExecute(true));
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
    }

    #region Mock Classes

    private class MockConfigurationRepository : IConfigurationRepository
    {
        private ApplicationSettings? _settings;
        public bool ShouldThrowOnLoad { get; set; }
        public bool ShouldThrowOnSave { get; set; }

        public void SetSettings(ApplicationSettings settings)
        {
            _settings = settings;
        }

        public ApplicationSettings? GetSavedSettings() => _settings;

        public Task<ApplicationSettings> LoadSettingsAsync()
        {
            if (ShouldThrowOnLoad)
                throw new InvalidOperationException("Mock repository error");

            return Task.FromResult(_settings ?? new ApplicationSettings());
        }

        public Task SaveSettingsAsync(ApplicationSettings settings)
        {
            if (ShouldThrowOnSave)
                throw new InvalidOperationException("Mock repository error");

            _settings = settings?.Clone();
            return Task.CompletedTask;
        }

        // Other interface methods (not used in settings tests)
        public Task<ApplicationConfiguration> LoadConfigurationAsync() => 
            Task.FromResult(new ApplicationConfiguration { Settings = _settings ?? new ApplicationSettings() });
        public Task SaveConfigurationAsync(ApplicationConfiguration configuration) => Task.CompletedTask;
        public Task<IEnumerable<ADAccount>> LoadAccountsAsync() => Task.FromResult(Enumerable.Empty<ADAccount>());
        public Task SaveAccountsAsync(IEnumerable<ADAccount> accounts) => Task.CompletedTask;
        public Task<IEnumerable<ExecutableConfiguration>> LoadExecutableConfigurationsAsync() => 
            Task.FromResult(Enumerable.Empty<ExecutableConfiguration>());
        public Task SaveExecutableConfigurationsAsync(IEnumerable<ExecutableConfiguration> configurations) => Task.CompletedTask;
        public Task<IEnumerable<NetworkDriveConfiguration>> LoadNetworkDriveConfigurationsAsync() =>
            Task.FromResult(Enumerable.Empty<NetworkDriveConfiguration>());
        public Task SaveNetworkDriveConfigurationsAsync(IEnumerable<NetworkDriveConfiguration> configurations) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class MockStartupRegistryService : IStartupRegistryService
    {
        public bool IsStartupEnabled { get; private set; } = true;
        public bool ShouldFailOnSet { get; set; }

        public Task<bool> IsStartupEnabledAsync() => Task.FromResult(IsStartupEnabled);

        public Task<bool> SetStartupEnabledAsync(bool enabled)
        {
            if (ShouldFailOnSet)
                return Task.FromResult(false);

            IsStartupEnabled = enabled;
            return Task.FromResult(true);
        }

        public Task<bool> EnableStartupAsync()
        {
            if (ShouldFailOnSet)
                return Task.FromResult(false);

            IsStartupEnabled = true;
            return Task.FromResult(true);
        }

        public Task<bool> DisableStartupAsync()
        {
            if (ShouldFailOnSet)
                return Task.FromResult(false);

            IsStartupEnabled = false;
            return Task.FromResult(true);
        }
    }

    private class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    #endregion
}