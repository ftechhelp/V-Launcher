using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Windows;
using V_Launcher.Resources;
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
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IClipboardService _clipboardService;
    private readonly IApplicationUpdateService _applicationUpdateService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private LauncherViewModel _launcherViewModel;

    [ObservableProperty]
    private CredentialManagementViewModel _credentialManagementViewModel;

    [ObservableProperty]
    private ExecutableManagementViewModel _executableManagementViewModel;

    [ObservableProperty]
    private AdHocLauncherViewModel _adHocLauncherViewModel;

    [ObservableProperty]
    private NetworkDriveManagementViewModel _networkDriveManagementVm;

    [ObservableProperty]
    private SettingsViewModel _settingsViewModel;

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

    public MainViewModel(
        ICredentialService credentialService,
        IExecutableService executableService,
        IProcessLauncher processLauncher,
        IConfigurationRepository configurationRepository,
        IClipboardService clipboardService,
        IApplicationUpdateService applicationUpdateService,
        INetworkDriveService networkDriveService,
        SettingsViewModel settingsViewModel,
        ILogger<MainViewModel> logger)
    {
        _credentialService = credentialService;
        _executableService = executableService;
        _processLauncher = processLauncher;
        _configurationRepository = configurationRepository;
        _clipboardService = clipboardService;
        _applicationUpdateService = applicationUpdateService ?? throw new ArgumentNullException(nameof(applicationUpdateService));
        _logger = logger;

        // Initialize child ViewModels
        _launcherViewModel = new LauncherViewModel(executableService, credentialService, processLauncher, configurationRepository, networkDriveService);
        _credentialManagementViewModel = new CredentialManagementViewModel(credentialService, RefreshDataAfterChangesAsync);
        _executableManagementViewModel = new ExecutableManagementViewModel(executableService, credentialService, RefreshDataAfterChangesAsync);
        _adHocLauncherViewModel = new AdHocLauncherViewModel(credentialService, executableService, processLauncher, clipboardService);
        _networkDriveManagementVm = new NetworkDriveManagementViewModel(networkDriveService, credentialService, RefreshDataAfterChangesAsync);
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));

        // Set initial view to launcher
        _currentViewModel = _launcherViewModel;

        // Initialize commands
        ShowLauncherViewCommand = new RelayCommand(ShowLauncherView, CanNavigate);
        ShowCredentialManagementViewCommand = new RelayCommand(ShowCredentialManagementView, CanNavigate);
        ShowExecutableManagementViewCommand = new RelayCommand(ShowExecutableManagementView, CanNavigate);
        ShowAdHocLauncherViewCommand = new RelayCommand(ShowAdHocLauncherView, CanNavigate);
        ShowNetworkDriveManagementViewCommand = new RelayCommand(ShowNetworkDriveManagementView, CanNavigate);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, CanNavigate);

        InitializeApplicationCommand = new AsyncRelayCommand(InitializeApplicationAsync);

        // Subscribe to child ViewModel events for status updates
        SubscribeToChildViewModelEvents();
        
        // Subscribe to launcher events
        _launcherViewModel.ApplicationLaunchedSuccessfully += OnApplicationLaunchedSuccessfully;

        // Initialize application
        _ = InitializeApplicationAsync();
    }

    #region Commands

    public IRelayCommand ShowLauncherViewCommand { get; }
    public IRelayCommand ShowCredentialManagementViewCommand { get; }
    public IRelayCommand ShowExecutableManagementViewCommand { get; }
    public IRelayCommand ShowAdHocLauncherViewCommand { get; }
    public IRelayCommand ShowNetworkDriveManagementViewCommand { get; }
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public IAsyncRelayCommand InitializeApplicationCommand { get; }

    // Settings commands exposed from SettingsViewModel
    public IAsyncRelayCommand<bool> ToggleStartOnWindowsStartCommand => SettingsViewModel.ToggleStartOnWindowsStartCommand;
    public IRelayCommand<bool> ToggleStartMinimizedCommand => SettingsViewModel.ToggleStartMinimizedCommand;
    public IRelayCommand<bool> ToggleMinimizeOnCloseCommand => SettingsViewModel.ToggleMinimizeOnCloseCommand;

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

    private void ShowAdHocLauncherView()
    {
        CurrentViewModel = AdHocLauncherViewModel;
        ClearStatus();
        _ = AdHocLauncherViewModel.LoadAccountsCommand.ExecuteAsync(null);
    }

    private void ShowNetworkDriveManagementView()
    {
        CurrentViewModel = NetworkDriveManagementVm;
        ClearStatus();
        _ = NetworkDriveManagementVm.LoadConfigurationsCommand.ExecuteAsync(null);
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

            // Initialize each ViewModel separately to isolate errors
            initializationTasks.Add(SafeExecuteAsync(
                () => SettingsViewModel.LoadSettingsCommand.ExecuteAsync(null),
                "settings initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => CredentialManagementViewModel.LoadAccountsCommand.ExecuteAsync(null),
                "credential management initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => ExecutableManagementViewModel.InitializeAsync(),
                "executable management initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => LauncherViewModel.LoadExecutablesCommand.ExecuteAsync(null),
                "launcher initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => AdHocLauncherViewModel.LoadAccountsCommand.ExecuteAsync(null),
                "ad hoc tools initialization"));

            initializationTasks.Add(SafeExecuteAsync(
                () => NetworkDriveManagementVm.LoadConfigurationsCommand.ExecuteAsync(null),
                "network drive management initialization"));

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

    private void OnApplicationLaunchedSuccessfully(object? sender, EventArgs e)
    {
        // Minimize to tray after successful application launch
        MinimizeMainWindowToTray();
    }

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

        AdHocLauncherViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(AdHocLauncherViewModel.StatusMessage) && CurrentViewModel == AdHocLauncherViewModel)
            {
                StatusMessage = AdHocLauncherViewModel.StatusMessage;
                HasError = AdHocLauncherViewModel.HasError;
            }
        };

        NetworkDriveManagementVm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(NetworkDriveManagementVm.ValidationMessage) && CurrentViewModel == NetworkDriveManagementVm)
            {
                StatusMessage = NetworkDriveManagementVm.ValidationMessage;
                HasError = NetworkDriveManagementVm.HasValidationError;
            }
        };

        // Subscribe to settings ViewModel events for status updates
        SettingsViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.StatusMessage))
            {
                StatusMessage = SettingsViewModel.StatusMessage;
                HasError = SettingsViewModel.HasError;
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
        else if (value == AdHocLauncherViewModel && !string.IsNullOrEmpty(AdHocLauncherViewModel.StatusMessage))
        {
            StatusMessage = AdHocLauncherViewModel.StatusMessage;
            HasError = AdHocLauncherViewModel.HasError;
        }
        else if (value == NetworkDriveManagementVm && !string.IsNullOrEmpty(NetworkDriveManagementVm.ValidationMessage))
        {
            StatusMessage = NetworkDriveManagementVm.ValidationMessage;
            HasError = NetworkDriveManagementVm.HasValidationError;
        }
    }

    partial void OnIsInitializingChanged(bool value)
    {
        // Update command can execute states when initialization state changes
        ShowLauncherViewCommand.NotifyCanExecuteChanged();
        ShowCredentialManagementViewCommand.NotifyCanExecuteChanged();
        ShowExecutableManagementViewCommand.NotifyCanExecuteChanged();
        ShowAdHocLauncherViewCommand.NotifyCanExecuteChanged();
        ShowNetworkDriveManagementViewCommand.NotifyCanExecuteChanged();
        CheckForUpdatesCommand.NotifyCanExecuteChanged();

    }



    #endregion





    #region Public Methods

    /// <summary>
    /// Handles application startup and configuration loading
    /// </summary>
    public async Task HandleApplicationStartupAsync()
    {
        await InitializeApplicationAsync();

        await CheckForUpdatesAsync();
        
        // Handle startup behavior after initialization
        HandleStartupBehavior();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            SetStatus(UpdateResources.UpdateCheckInProgress);

            var updateResult = await _applicationUpdateService.CheckForUpdatesAsync();
            if (!updateResult.IsUpdateAvailable)
            {
                SetStatus(UpdateResources.UpdateNoUpdateMessage);
                return;
            }

            if (updateResult.LatestVersion is null)
            {
                SetError(UpdateResources.UpdateCheckFailedMessage);
                return;
            }

            var promptMessage = string.Format(UpdateResources.UpdateAvailablePromptBody, updateResult.LatestVersion);
            var result = System.Windows.MessageBox.Show(
                promptMessage,
                UpdateResources.UpdateAvailablePromptTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
            {
                SetStatus(UpdateResources.UpdateNoUpdateMessage);
                return;
            }

            var installerStarted = await _applicationUpdateService.InstallUpdateAsync(updateResult);
            if (!installerStarted)
            {
                SetError(UpdateResources.UpdateInstallFailedMessage);
                return;
            }

            SetStatus(UpdateResources.UpdateInstallStartedMessage);
            System.Windows.Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update check was canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update check.");
            SetError(UpdateResources.UpdateCheckFailedMessage);
        }
    }

    /// <summary>
    /// Handles startup behavior based on application settings
    /// </summary>
    private void HandleStartupBehavior()
    {
        try
        {
            // If starting minimized, the App.xaml.cs already handles the window state
            // We just need to log the behavior
            if (SettingsViewModel.Settings.StartMinimized)
            {
                _logger.LogInformation("Application started minimized to system tray");
                SetStatus("Application started minimized to system tray");
                
                // Clear status after a delay
                _ = Task.Delay(3000).ContinueWith(_ => 
                {
                    if (!IsDisposed)
                    {
                        ClearStatus();
                    }
                });
            }
            else
            {
                _logger.LogInformation("Application started normally");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling startup behavior");
        }
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

            await AdHocLauncherViewModel.LoadAccountsCommand.ExecuteAsync(null);

            await NetworkDriveManagementVm.LoadConfigurationsCommand.ExecuteAsync(null);
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

    #region Window Management

    /// <summary>
    /// Event to notify when the window should be shown from tray
    /// </summary>
    public event EventHandler? ShowWindowRequested;

    /// <summary>
    /// Event to notify when the window should be minimized to tray
    /// </summary>
    public event EventHandler? MinimizeToTrayRequested;

    /// <summary>
    /// Shows the main window (used by system tray interactions)
    /// </summary>
    public void ShowMainWindow()
    {
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Minimizes the main window to tray (used by programmatic requests)
    /// </summary>
    public void MinimizeMainWindowToTray()
    {
        MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        try
        {
            _logger.LogDebug("Disposing MainViewModel");

            // Unsubscribe from events
            if (LauncherViewModel != null)
            {
                LauncherViewModel.ApplicationLaunchedSuccessfully -= OnApplicationLaunchedSuccessfully;
            }

            // Dispose child ViewModels safely
            try { LauncherViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing LauncherViewModel"); }
            try { CredentialManagementViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing CredentialManagementViewModel"); }
            try { ExecutableManagementViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing ExecutableManagementViewModel"); }
            try { AdHocLauncherViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing AdHocLauncherViewModel"); }
            try { NetworkDriveManagementVm?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing NetworkDriveManagementViewModel"); }
            try { SettingsViewModel?.Dispose(); } catch (Exception ex) { _logger.LogError(ex, "Error disposing SettingsViewModel"); }

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