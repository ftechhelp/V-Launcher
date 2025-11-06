using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public MainViewModel(
        ICredentialService credentialService,
        IExecutableService executableService,
        IProcessLauncher processLauncher)
    {
        _credentialService = credentialService;
        _executableService = executableService;
        _processLauncher = processLauncher;

        // Initialize child ViewModels
        _launcherViewModel = new LauncherViewModel(executableService, credentialService, processLauncher);
        _credentialManagementViewModel = new CredentialManagementViewModel(credentialService);
        _executableManagementViewModel = new ExecutableManagementViewModel(executableService, credentialService);

        // Set initial view to launcher
        _currentViewModel = _launcherViewModel;

        // Initialize commands
        ShowLauncherViewCommand = new RelayCommand(ShowLauncherView, CanNavigate);
        ShowCredentialManagementViewCommand = new RelayCommand(ShowCredentialManagementView, CanNavigate);
        ShowExecutableManagementViewCommand = new RelayCommand(ShowExecutableManagementView, CanNavigate);
        RefreshAllDataCommand = new AsyncRelayCommand(RefreshAllDataAsync, CanRefreshData);
        InitializeApplicationCommand = new AsyncRelayCommand(InitializeApplicationAsync);

        // Subscribe to child ViewModel events for status updates
        SubscribeToChildViewModelEvents();

        // Initialize application
        _ = InitializeApplicationAsync();
    }

    #region Commands

    public IRelayCommand ShowLauncherViewCommand { get; }
    public IRelayCommand ShowCredentialManagementViewCommand { get; }
    public IRelayCommand ShowExecutableManagementViewCommand { get; }
    public IAsyncRelayCommand RefreshAllDataCommand { get; }
    public IAsyncRelayCommand InitializeApplicationCommand { get; }

    #endregion

    #region Navigation Commands

    private void ShowLauncherView()
    {
        CurrentViewModel = LauncherViewModel;
        ClearStatus();
    }

    private void ShowCredentialManagementView()
    {
        CurrentViewModel = CredentialManagementViewModel;
        ClearStatus();
    }

    private void ShowExecutableManagementView()
    {
        CurrentViewModel = ExecutableManagementViewModel;
        ClearStatus();
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

            // Load initial data for all ViewModels
            await Task.WhenAll(
                LauncherViewModel.LoadExecutablesCommand.ExecuteAsync(null),
                CredentialManagementViewModel.LoadAccountsCommand.ExecuteAsync(null),
                ExecutableManagementViewModel.LoadConfigurationsCommand.ExecuteAsync(null),
                ExecutableManagementViewModel.InitializeAsync()
            );

            SetStatus("Application initialized successfully");
            
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
            SetError($"Failed to initialize application: {ex.Message}");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private async Task RefreshAllDataAsync()
    {
        try
        {
            SetStatus("Refreshing all data...");

            // Refresh data in all ViewModels
            await Task.WhenAll(
                LauncherViewModel.RefreshExecutablesCommand.ExecuteAsync(null),
                CredentialManagementViewModel.LoadAccountsCommand.ExecuteAsync(null),
                ExecutableManagementViewModel.LoadConfigurationsCommand.ExecuteAsync(null)
            );

            SetStatus("All data refreshed successfully");
            
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
            SetError($"Failed to refresh data: {ex.Message}");
        }
    }

    private bool CanRefreshData() => !IsInitializing;

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
        RefreshAllDataCommand.NotifyCanExecuteChanged();
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
            // Perform any necessary cleanup operations
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Log shutdown errors but don't throw
            System.Diagnostics.Debug.WriteLine($"Error during application shutdown: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes data when configurations change (e.g., after adding/editing accounts or executables)
    /// </summary>
    public async Task RefreshDataAfterChangesAsync()
    {
        // Refresh launcher data when accounts or executable configurations change
        await LauncherViewModel.RefreshExecutablesCommand.ExecuteAsync(null);
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        // Dispose child ViewModels
        LauncherViewModel?.Dispose();
        CredentialManagementViewModel?.Dispose();
        ExecutableManagementViewModel?.Dispose();

        base.OnDisposing();
    }

    #endregion
}