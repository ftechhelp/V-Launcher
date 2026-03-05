using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V_Launcher.Models;
using V_Launcher.Properties;
using V_Launcher.Services;

namespace V_Launcher.ViewModels;

/// <summary>
/// ViewModel for displaying and launching executables with AD credentials
/// </summary>
public partial class LauncherViewModel : ViewModelBase
{
    private readonly IExecutableService _executableService;
    private readonly ICredentialService _credentialService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly INetworkDriveService _networkDriveService;

    [ObservableProperty]
    private ObservableCollection<ExecutableItem> _executableItems = new();

    [ObservableProperty]
    private ExecutableItem? _selectedExecutableItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isLaunching;

    [ObservableProperty]
    private LauncherOrderMode _launcherOrderMode = LauncherOrderMode.Custom;

    [ObservableProperty]
    private ObservableCollection<NetworkDriveItem> _networkDriveItems = new();

    [ObservableProperty]
    private bool _isOpeningNetworkDrive;

    private bool _isRefreshing;
    private bool _isLoadingOrderMode;

    public LauncherViewModel(
        IExecutableService executableService,
        ICredentialService credentialService,
        IProcessLauncher processLauncher,
        IConfigurationRepository configurationRepository,
        INetworkDriveService networkDriveService)
    {
        _executableService = executableService;
        _credentialService = credentialService;
        _processLauncher = processLauncher;
        _configurationRepository = configurationRepository;
        _networkDriveService = networkDriveService;

        LaunchExecutableCommand = new AsyncRelayCommand<ExecutableItem>(LaunchExecutableAsync, CanLaunchExecutable);
        RefreshExecutablesCommand = new AsyncRelayCommand(RefreshExecutablesAsync, () => !IsLoading);
        LoadExecutablesCommand = new AsyncRelayCommand(LoadExecutablesAsync);
        OpenNetworkDriveCommand = new AsyncRelayCommand<NetworkDriveItem>(OpenNetworkDriveAsync, CanOpenNetworkDrive);

        // Load executables on initialization
        _ = InitializeAsync();
    }

    #region Commands

    public IAsyncRelayCommand<ExecutableItem> LaunchExecutableCommand { get; }
    public IAsyncRelayCommand RefreshExecutablesCommand { get; }
    public IAsyncRelayCommand LoadExecutablesCommand { get; }
    public IAsyncRelayCommand<NetworkDriveItem> OpenNetworkDriveCommand { get; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when an application is successfully launched
    /// </summary>
    public event EventHandler? ApplicationLaunchedSuccessfully;

    public bool IsCustomOrderMode => LauncherOrderMode == LauncherOrderMode.Custom;

    #endregion

    #region Command Implementations

    private async Task LaunchExecutableAsync(ExecutableItem? executableItem)
    {
        if (executableItem?.Configuration == null || executableItem.Account == null)
        {
            SetError("Invalid executable configuration or account.");
            return;
        }

        try
        {
            IsLaunching = true;
            ClearStatus();
            SetStatus($"Launching {executableItem.Configuration.DisplayName}...");

            // Decrypt the password for the associated account
            var password = await _credentialService.DecryptPasswordAsync(executableItem.Account);

            // Validate configuration and credentials before launching
            if (!_processLauncher.ValidateConfiguration(executableItem.Configuration))
            {
                SetError($"Invalid configuration for {executableItem.Configuration.DisplayName}.");
                return;
            }

            if (!_processLauncher.ValidateCredentials(executableItem.Account, password))
            {
                SetError($"Invalid credentials for account {executableItem.Account.DisplayName}.");
                return;
            }

            // Launch the process
            var success = await _processLauncher.LaunchAsync(
                executableItem.Configuration, 
                executableItem.Account, 
                password);

            if (success)
            {
                SetStatus($"Successfully launched {executableItem.Configuration.DisplayName}");
                
                // Trigger minimize to tray after successful launch
                ApplicationLaunchedSuccessfully?.Invoke(this, EventArgs.Empty);
                
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
                SetError($"Failed to launch {executableItem.Configuration.DisplayName}. Please check the executable path and credentials.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            SetError($"Access denied when launching {executableItem.Configuration.DisplayName}. Please check your credentials and permissions.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            SetError($"Windows error launching {executableItem.Configuration.DisplayName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetError($"Unexpected error launching {executableItem.Configuration.DisplayName}: {ex.Message}");
        }
        finally
        {
            IsLaunching = false;
        }
    }

    private async Task RefreshExecutablesAsync()
    {
        await LoadExecutablesAsync();
        await LoadNetworkDrivesAsync();
        SetStatus("Executable list refreshed");
        
        // Clear status after a delay
        _ = Task.Delay(2000).ContinueWith(_ => 
        {
            if (!IsDisposed)
            {
                ClearStatus();
            }
        });
    }

    private async Task OpenNetworkDriveAsync(NetworkDriveItem? driveItem)
    {
        if (driveItem?.Configuration == null || driveItem.Account == null)
        {
            SetError("Invalid network drive configuration or account.");
            return;
        }

        try
        {
            IsOpeningNetworkDrive = true;
            ClearStatus();
            SetStatus($"Opening {driveItem.Configuration.DisplayName}...");

            var password = await _credentialService.DecryptPasswordAsync(driveItem.Account);
            await _networkDriveService.OpenDriveAsync(driveItem.Configuration, driveItem.Account, password);

            SetStatus($"Opened {driveItem.Configuration.DisplayName}");

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
            SetError($"Failed to open network drive: {ex.Message}");
        }
        finally
        {
            IsOpeningNetworkDrive = false;
        }
    }

    private async Task LoadNetworkDrivesAsync()
    {
        try
        {
            var configurations = await _networkDriveService.GetConfigurationsAsync();
            var accounts = await _credentialService.GetAccountsAsync();
            var accountLookup = accounts.ToDictionary(item => item.Id, item => item);

            var networkDriveItems = configurations
                .Where(configuration => accountLookup.ContainsKey(configuration.ADAccountId))
                .Select(configuration => new NetworkDriveItem
                {
                    Configuration = configuration,
                    Account = accountLookup[configuration.ADAccountId]
                })
                .ToList();

            await InvokeOnUIThreadAsync(() =>
            {
                NetworkDriveItems.Clear();
                foreach (var item in networkDriveItems)
                {
                    NetworkDriveItems.Add(item);
                }
            });
        }
        catch (Exception ex)
        {
            SetError($"Failed to load network drives: {ex.Message}");
        }
    }

    private async Task LoadExecutablesAsync()
    {
        // Prevent concurrent refreshes
        if (_isRefreshing) return;
        
        try
        {
            _isRefreshing = true;
            IsLoading = true;
            ClearStatus();

            // Load configurations and accounts
            var configurations = await _executableService.GetConfigurationsAsync();
            var accounts = await _credentialService.GetAccountsAsync();
            var accountsDict = accounts.ToDictionary(a => a.Id, a => a);

            // Create executable items with icons (off UI thread)
            var executableItems = new List<ExecutableItem>();
            foreach (var config in configurations)
            {
                if (accountsDict.TryGetValue(config.ADAccountId, out var account))
                {
                    var executableItem = new ExecutableItem
                    {
                        Configuration = config,
                        Account = account
                    };

                    // Load icon asynchronously
                    try
                    {
                        executableItem.Icon = await _executableService.GetIconAsync(config);
                    }
                    catch
                    {
                        // Icon loading failed, will use default icon
                        executableItem.Icon = null;
                    }

                    executableItems.Add(executableItem);
                }
            }

            var orderedItems = ApplyOrdering(executableItems);

            // Update collections on UI thread
            await InvokeOnUIThreadAsync(() =>
            {
                ExecutableItems.Clear();
                foreach (var item in orderedItems)
                {
                    ExecutableItems.Add(item);
                }
            });

            if (!executableItems.Any())
            {
                SetStatus("No executable configurations found. Use the configuration management to add executables.");
            }

            await LoadNetworkDrivesAsync();
        }
        catch (Exception ex)
        {
            SetError($"Failed to load executables: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _isRefreshing = false;
        }
    }

    private async Task InitializeAsync()
    {
        await LoadOrderModeAsync();
        await LoadExecutablesAsync();
    }

    private async Task LoadOrderModeAsync()
    {
        try
        {
            _isLoadingOrderMode = true;
            var settings = await _configurationRepository.LoadSettingsAsync();
            LauncherOrderMode = settings.LauncherOrderMode;
        }
        finally
        {
            _isLoadingOrderMode = false;
        }
    }

    private List<ExecutableItem> ApplyOrdering(IEnumerable<ExecutableItem> executableItems)
    {
        if (LauncherOrderMode == LauncherOrderMode.Alphabetical)
        {
            return executableItems
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return executableItems.ToList();
    }

    public async Task SetLauncherOrderModeAsync(LauncherOrderMode orderMode)
    {
        try
        {
            if (LauncherOrderMode != orderMode)
            {
                LauncherOrderMode = orderMode;
            }

            var settings = await _configurationRepository.LoadSettingsAsync();
            settings.LauncherOrderMode = orderMode;
            await _configurationRepository.SaveSettingsAsync(settings);

            if (orderMode == LauncherOrderMode.Custom)
            {
                await LoadExecutablesAsync();
            }
            else
            {
                var orderedItems = ApplyOrdering(ExecutableItems);
                await InvokeOnUIThreadAsync(() =>
                {
                    ExecutableItems.Clear();
                    foreach (var item in orderedItems)
                    {
                        ExecutableItems.Add(item);
                    }
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            SetError(string.Format(global::V_Launcher.Properties.Resources.LauncherOrderModeUpdateFailed, ex.Message));
        }
    }

    public async Task MoveExecutableItemAsync(ExecutableItem sourceItem, ExecutableItem? targetItem)
    {
        ArgumentNullException.ThrowIfNull(sourceItem);

        if (!IsCustomOrderMode)
        {
            return;
        }

        var sourceIndex = ExecutableItems.IndexOf(sourceItem);
        if (sourceIndex < 0)
        {
            return;
        }

        var targetIndex = targetItem == null
            ? ExecutableItems.Count - 1
            : ExecutableItems.IndexOf(targetItem);

        if (targetIndex < 0 || targetIndex == sourceIndex)
        {
            return;
        }

        try
        {
            await InvokeOnUIThreadAsync(() => ExecutableItems.Move(sourceIndex, targetIndex));

            var orderedConfigurationIds = ExecutableItems
                .Select(item => item.Configuration?.Id)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            await _executableService.SaveConfigurationOrderAsync(orderedConfigurationIds);
        }
        catch (InvalidOperationException ex)
        {
            SetError(string.Format(global::V_Launcher.Properties.Resources.LauncherOrderSaveFailed, ex.Message));
        }
        }

    #endregion

    #region Command Can Execute Methods

    private bool CanLaunchExecutable(ExecutableItem? executableItem)
    {
        return !IsLoading && !IsLaunching && executableItem?.Configuration != null && executableItem.Account != null;
    }

    private bool CanOpenNetworkDrive(NetworkDriveItem? networkDriveItem)
    {
        return !IsLoading && !IsOpeningNetworkDrive && networkDriveItem?.Configuration != null && networkDriveItem.Account != null;
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

    partial void OnIsLoadingChanged(bool value)
    {
        RefreshExecutablesCommand.NotifyCanExecuteChanged();
        LaunchExecutableCommand.NotifyCanExecuteChanged();
        OpenNetworkDriveCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLaunchingChanged(bool value)
    {
        LaunchExecutableCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsOpeningNetworkDriveChanged(bool value)
    {
        OpenNetworkDriveCommand.NotifyCanExecuteChanged();
    }

    partial void OnLauncherOrderModeChanged(LauncherOrderMode value)
    {
        OnPropertyChanged(nameof(IsCustomOrderMode));
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        // Clear collections and dispose of any resources
        ExecutableItems.Clear();
        NetworkDriveItems.Clear();
        base.OnDisposing();
    }

    #endregion
}

/// <summary>
/// Represents a network drive item with its configuration and associated account.
/// </summary>
public class NetworkDriveItem
{
    public NetworkDriveConfiguration? Configuration { get; set; }
    public ADAccount? Account { get; set; }

    public string DisplayName => Configuration?.DisplayName ?? "Unknown";
    public string RemotePath => Configuration?.RemotePath ?? string.Empty;
    public string AccountDisplayName => Account?.DisplayName ?? "Unknown Account";
}

/// <summary>
/// Represents an executable item with its configuration, associated account, and icon
/// </summary>
public class ExecutableItem
{
    public ExecutableConfiguration? Configuration { get; set; }
    public ADAccount? Account { get; set; }
    public BitmapImage? Icon { get; set; }

    public string DisplayName => Configuration?.DisplayName ?? "Unknown";
    public string AccountDisplayName => Account?.DisplayName ?? "Unknown Account";
    public string ExecutablePath => Configuration?.ExecutablePath ?? string.Empty;
}