using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using V_Launcher.Services;

namespace V_Launcher.ViewModels;

/// <summary>
/// Main ViewModel that coordinates between different view models and manages application state
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ICredentialService _credentialService;
    private readonly IExecutableService _executableService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IStartupRegistryService _startupRegistryService;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private LauncherViewModel _launcherViewModel;

    [ObservableProperty]
    private CredentialManagementViewModel _credentialManagementViewModel;

    [ObservableProperty]
    private ExecutableManagementViewModel _executableManagementViewModel;

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    [ObservableProperty]
    private bool _isInitializing;

    [ObservableProperty]
    private string _applicationTitle = "AD User Launcher";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private Models.ApplicationSettings _applicationSettings = new();

    public MainViewModel(
        ICredentialService credentialService,
        IExecutableService executableService,
        IProcessLauncher processLauncher,
        IStartupRegistryService startupRegistryService,
        IConfigurationRepository configurationRepository,
        ILogger<MainViewModel> logger)
    {
        _credentialService = credentialService;
        _executableService = executableService;
        _processLauncher = processLauncher;
        _startupRegistryService = startupRegistryService;
        _configurationRepository = configurationRepository;
        _logger = logger;

        // Initialize child ViewModels
        _launcherViewModel = new LauncherViewModel(executableService, credentialService, processLauncher);
        _credentialManagementViewModel = new CredentialManagementViewModel(credentialService, RefreshDataAfterChangesAsync);
        _executableManagementViewModel = new ExecutableManagementViewModel(executableService, credentialService, RefreshDataAfterChangesAsync);

        // Set initial view to launcher
        _currentViewModel = _launcherViewModel;

        // Initialize commands
        ShowLauncherViewCommand = new RelayCommand(ShowLauncherView, CanNavigate);
        ShowCredentialManagementViewCommand = new RelayCommand(ShowCredentialManagementView, CanNavigate);
        ShowExecutableManagementViewCommand = new RelayCommand(ShowExecutableManagementView, CanNavigate);

        InitializeApplicationCommand = new AsyncRelayCommand(InitializeApplicationAsync);
        ToggleStartOnWindowsStartCommand = new AsyncRelayCommand<bool>(ToggleStartOnWindowsStartAsync);
        ToggleStartMinimizedCommand = new RelayCommand<bool>(ToggleStartMinimized);
        ToggleMinimizeOnCloseCommand = new RelayCommand<bool>(ToggleMinimizeOnClose);

        // Subscribe to child ViewModel events for status updates
        SubscribeToChildViewModelEvents();

        // Initialize application
        _ = InitializeApplicationAsync();
    }

    #region Commands

    public IRelayCommand ShowLauncherViewCommand { get; }
    public IRelayCommand ShowCredentialManagementViewCommand { get; }
    public IRelayCommand ShowExecutableManagementViewCommand { get; }

    public IAsyncRelayCommand InitializeApplicationCommand { get; }
    public IAsyncRelayCommand<bool> ToggleStartOnWindowsStartCommand { get; }
    public IRelayCommand<bool> ToggleStartMinimizedCommand { get; }
    public IRelayCommand<bool> ToggleMinimizeOnCloseCommand { get; }

    #endregion

    #region Navigation Commands

    private void ShowLauncherView()
    {
        CurrentViewModel = LauncherViewModel;
        ClearStatus();
        // Automatically refresh launcher data when switching to this view
        _ = LauncherViewModel.LoadExecutablesCommand.ExecuteAsync(null);
    }

    private void ShowCredentialManagementView()
    {
        CurrentViewModel = CredentialManagementViewModel;
        ClearStatus();
        // Automatically refresh credential data when switching to this view
        _ = CredentialManagementViewModel.LoadAccountsCommand.ExecuteAsync(null);
    }

    private void ShowExecutableManagementView()
    {
        CurrentViewModel = ExecutableManagementViewModel;
        ClearStatus();
        // Automatically refresh executable management data when switching to this view
        _ = RefreshExecutableManagementDataAsync();
    }

    private async Task RefreshExecutableManagementDataAsync()
    {
        await ExecutableManagementViewModel.RefreshAvailableAccountsAsync();
        await ExecutableManagementViewModel.LoadConfigurationsCommand.ExecuteAsync(null);
    }

    private bool CanNavigate() => !IsInitializing;

    #endregion

    #region Application Lifecycle

    private async Task InitializeApplicationAsync()
    {
        try
        {
            IsInitializing = true;
            SetStatus("Initializing application...");
            _logger.LogInformation("Starting application initialization");

            // Load initial data for all ViewModels with individual error handling
            var initializationTasks = new List<Task>();

            // Load application settings first
            await SafeExecuteAsync(
                () => LoadApplicationSettingsAsync(),
                "application settings initialization");

            // Initialize each ViewModel separately to isolate errors
            initializationTasks.Add(SafeExecuteAsync(
                () => CredentialManagementViewModel.LoadAccountsCommand.ExecuteAsync(null),
                "credential management initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => ExecutableManagementViewModel.InitializeAsync(),
                "executable management initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => LauncherViewModel.LoadExecutablesCommand.ExecuteAsync(null),
                "launcher initialization"));

            // Wait for all initialization tasks to complete
            await Task.WhenAll(initializationTasks);

            SetStatus("Application initialized successfully");
            _logger.LogInformation("Application initialization completed successfully");
            
            // Clear status after a delay
            _ = Task.Delay(2000).ContinueWith(_ => 
            {
                if (!IsDisposed)
                {
                    ClearStatus();
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during application initialization");
            SetError($"Failed to initialize application: {ex.Message}");
        }
        finally
        {
            IsInitializing = false;
        }
    }



    #endregion

    #region Child ViewModel Event Handling

    private void SubscribeToChildViewModelEvents()
    {
        // Subscribe to property changes in child ViewModels to propagate status updates
        LauncherViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(LauncherViewModel.StatusMessage) && CurrentViewModel == LauncherViewModel)
            {
                StatusMessage = LauncherViewModel.StatusMessage;
                HasError = LauncherViewModel.HasError;
            }
        };

        CredentialManagementViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(CredentialManagementViewModel.ValidationMessage) && CurrentViewModel == CredentialManagementViewModel)
            {
                StatusMessage = CredentialManagementViewModel.ValidationMessage;
                HasError = CredentialManagementViewModel.HasValidationError;
            }
        };

        ExecutableManagementViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ExecutableManagementViewModel.ValidationMessage) && CurrentViewModel == ExecutableManagementViewModel)
            {
                StatusMessage = ExecutableManagementViewModel.ValidationMessage;
                HasError = ExecutableManagementViewModel.HasValidationError;
            }
        };
    }

    #endregion

    #region Status and Error Handling

    private void SetStatus(string message)
    {
        StatusMessage = message;
        HasError = false;
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        HasError = true;
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        HasError = false;
    }

    #endregion

    #region Property Change Notifications

    partial void OnCurrentViewModelChanged(ViewModelBase value)
    {
        // Clear status when switching views
        ClearStatus();
        
        // Update status from the new current ViewModel if it has any
        if (value == LauncherViewModel && !string.IsNullOrEmpty(LauncherViewModel.StatusMessage))
        {
            StatusMessage = LauncherViewModel.StatusMessage;
            HasError = LauncherViewModel.HasError;
        }
        else if (value == CredentialManagementViewModel && !string.IsNullOrEmpty(CredentialManagementViewModel.ValidationMessage))
        {
            StatusMessage = CredentialManagementViewModel.ValidationMessage;
            HasError = CredentialManagementViewModel.HasValidationError;
        }
        else if (value == ExecutableManagementViewModel && !string.IsNullOrEmpty(ExecutableManagementViewModel.ValidationMessage))
        {
            StatusMessage = ExecutableManagementViewModel.ValidationMessage;
            HasError = ExecutableManagementViewModel.HasValidationError;
        }
    }

    partial void OnIsInitializingChanged(bool value)
    {
        // Update command can execute states when initialization state changes
        ShowLauncherViewCommand.NotifyCanExecuteChanged();
        ShowCredentialManagementViewCommand.NotifyCanExecuteChanged();
        ShowExecutableManagementViewCommand.NotifyCanExecuteChanged();

    }

    partial void OnApplicationSettingsChanged(Models.ApplicationSettings value)
    {
        // Save settings when they change (but not during initialization)
        if (!IsInitializing)
        {
            _ = SaveApplicationSettingsAsync();
        }
    }

    #endregion

    #region Settings Commands

    /// <summary>
    /// Toggles the Start with Windows setting
    /// </summary>
    private async Task ToggleStartOnWindowsStartAsync(bool enabled)
    {
        await SetStartOnWindowsStartAsync(enabled);
    }

    /// <summary>
    /// Toggles the Start Minimized setting
    /// </summary>
    private void ToggleStartMinimized(bool enabled)
    {
        ApplicationSettings.StartMinimized = enabled;
        SetStatus($"Start minimized {(enabled ? "enabled" : "disabled")}");
        
        // Clear status after a delay
        _ = Task.Delay(2000).ContinueWith(_ => 
        {
            if (!IsDisposed)
            {
                ClearStatus();
            }
        });
    }

    /// <summary>
    /// Toggles the Minimize on Close setting
    /// </summary>
    private void ToggleMinimizeOnClose(bool enabled)
    {
        ApplicationSettings.MinimizeOnClose = enabled;
        SetStatus($"Minimize on close {(enabled ? "enabled" : "disabled")}");
        
        // Clear status after a delay
        _ = Task.Delay(2000).ContinueWith(_ => 
        {
            if (!IsDisposed)
            {
                ClearStatus();
            }
        });
    }

    #endregion

    #region Application Settings Management

    /// <summary>
    /// Loads application settings from configuration
    /// </summary>
    private async Task LoadApplicationSettingsAsync()
    {
        try
        {
            var settings = await _configurationRepository.LoadSettingsAsync();
            ApplicationSettings = settings;
            
            // Sync registry state with loaded settings
            await SyncStartupRegistryStateAsync();
            
            _logger.LogDebug("Application settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application settings");
            // Use default settings on error
            ApplicationSettings = new Models.ApplicationSettings();
        }
    }

    /// <summary>
    /// Saves application settings to configuration
    /// </summary>
    private async Task SaveApplicationSettingsAsync()
    {
        try
        {
            await _configurationRepository.SaveSettingsAsync(ApplicationSettings);
            _logger.LogDebug("Application settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application settings");
            SetError($"Failed to save settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles changes to the StartOnWindowsStart setting
    /// </summary>
    /// <param name="enabled">Whether startup should be enabled</param>
    public async Task SetStartOnWindowsStartAsync(bool enabled)
    {
        try
        {
            var success = await _startupRegistryService.SetStartupEnabledAsync(enabled);
            
            if (success)
            {
                ApplicationSettings.StartOnWindowsStart = enabled;
                await SaveApplicationSettingsAsync();
                
                var status = enabled ? "enabled" : "disabled";
                SetStatus($"Windows startup {status} successfully");
                _logger.LogInformation("Windows startup setting changed to {Enabled}", enabled);
            }
            else
            {
                SetError("Failed to update Windows startup setting. You may need administrator privileges.");
                _logger.LogWarning("Failed to update Windows startup registry setting");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Windows startup setting");
            SetError($"Error updating startup setting: {ex.Message}");
        }
    }

    /// <summary>
    /// Syncs the registry state with the current application settings
    /// </summary>
    private async Task SyncStartupRegistryStateAsync()
    {
        try
        {
            var registryEnabled = await _startupRegistryService.IsStartupEnabledAsync();
            
            // If there's a mismatch, update the registry to match the settings
            if (registryEnabled != ApplicationSettings.StartOnWindowsStart)
            {
                var success = await _startupRegistryService.SetStartupEnabledAsync(ApplicationSettings.StartOnWindowsStart);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to sync startup registry state with application settings");
                    // Update the setting to match the actual registry state
                    ApplicationSettings.StartOnWindowsStart = registryEnabled;
                    await SaveApplicationSettingsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing startup registry state");
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Handles application startup and configuration loading
    /// </summary>
    public async Task HandleApplicationStartupAsync()
    {
        await InitializeApplicationAsync();
    }

    /// <summary>
    /// Handles application shutdown and cleanup
    /// </summary>
    public async Task HandleApplicationShutdownAsync()
    {
        try
        {
            _logger.LogInformation("Starting application shutdown cleanup");

            // Perform cleanup operations for each ViewModel
            var cleanupTasks = new List<Task>();

            // Ensure all ViewModels are properly disposed
            if (LauncherViewModel != null)
            {
                cleanupTasks.Add(SafeExecuteAsync(
                    () => Task.Run(() => LauncherViewModel.Dispose()),
                    "launcher cleanup"));
            }

            if (CredentialManagementViewModel != null)
            {
                cleanupTasks.Add(SafeExecuteAsync(
                    () => Task.Run(() => CredentialManagementViewModel.Dispose()),
                    "credential management cleanup"));
            }

            if (ExecutableManagementViewModel != null)
            {
                cleanupTasks.Add(SafeExecuteAsync(
                    () => Task.Run(() => ExecutableManagementViewModel.Dispose()),
                    "executable management cleanup"));
            }

            // Wait for all cleanup tasks to complete
            await Task.WhenAll(cleanupTasks);

            _logger.LogInformation("Application shutdown cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application shutdown cleanup");
            // Don't rethrow - we're shutting down anyway
        }
    }

    /// <summary>
    /// Refreshes data when configurations change (e.g., after adding/editing accounts or executables)
    /// </summary>
    public async Task RefreshDataAfterChangesAsync()
    {
        try
        {
            // Refresh launcher data when accounts or executable configurations change
            await LauncherViewModel.LoadExecutablesCommand.ExecuteAsync(null);
            
            // Also refresh executable management data to ensure account list is up to date
            await ExecutableManagementViewModel.RefreshAvailableAccountsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing data after changes");
        }
    }

    #endregion

    #region Error Handling Helpers

    /// <summary>
    /// Safely executes an async operation with error handling and logging
    /// </summary>
    private async Task SafeExecuteAsync(Func<Task> operation, string operationName)
    {
        try
        {
            await operation();
            _logger.LogDebug("Successfully completed {OperationName}", operationName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {OperationName}", operationName);
            
            // Don't rethrow - we want to continue with other operations
            // The individual ViewModels will handle their own error states
        }
    }

    /// <summary>
    /// Safely executes an async operation with error handling, logging, and return value
    /// </summary>
    private async Task<T?> SafeExecuteAsync<T>(Func<Task<T>> operation, string operationName, T? defaultValue = default)
    {
        try
        {
            var result = await operation();
            _logger.LogDebug("Successfully completed {OperationName}", operationName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {OperationName}", operationName);
            return defaultValue;
        }
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        try
        {
            _logger.LogDebug("Disposing MainViewModel");

            // Dispose child ViewModels safely
            try { LauncherViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing LauncherViewModel"); }
            try { CredentialManagementViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing CredentialManagementViewModel"); }
            try { ExecutableManagementViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing ExecutableManagementViewModel"); }

            _logger.LogDebug("MainViewModel disposal completed");
        }
        catch (Exception ex)
        {
            // Last resort error handling - use Debug.WriteLine since logger might be disposed
            System.Diagnostics.Debug.WriteLine($"Error during MainViewModel disposal: {ex.Message}");
        }
        finally
        {
            base.OnDisposing();
        }
    }

    #endregion
}