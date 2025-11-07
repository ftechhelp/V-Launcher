using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V_Launcher.Models;
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

    private bool _isRefreshing;

    public LauncherViewModel(
        IExecutableService executableService,
        ICredentialService credentialService,
        IProcessLauncher processLauncher)
    {
        _executableService = executableService;
        _credentialService = credentialService;
        _processLauncher = processLauncher;

        LaunchExecutableCommand = new AsyncRelayCommand<ExecutableItem>(LaunchExecutableAsync, CanLaunchExecutable);
        RefreshExecutablesCommand = new AsyncRelayCommand(RefreshExecutablesAsync, () => !IsLoading);
        LoadExecutablesCommand = new AsyncRelayCommand(LoadExecutablesAsync);

        // Load executables on initialization
        _ = LoadExecutablesAsync();
    }

    #region Commands

    public IAsyncRelayCommand<ExecutableItem> LaunchExecutableCommand { get; }
    public IAsyncRelayCommand RefreshExecutablesCommand { get; }
    public IAsyncRelayCommand LoadExecutablesCommand { get; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when an application is successfully launched
    /// </summary>
    public event EventHandler? ApplicationLaunchedSuccessfully;

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

            // Update collections on UI thread
            await InvokeOnUIThreadAsync(() =>
            {
                ExecutableItems.Clear();
                foreach (var item in executableItems)
                {
                    ExecutableItems.Add(item);
                }
            });

            if (!executableItems.Any())
            {
                SetStatus("No executable configurations found. Use the configuration management to add executables.");
            }
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

    #endregion

    #region Command Can Execute Methods

    private bool CanLaunchExecutable(ExecutableItem? executableItem)
    {
        return !IsLoading && !IsLaunching && executableItem?.Configuration != null && executableItem.Account != null;
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
    }

    partial void OnIsLaunchingChanged(bool value)
    {
        LaunchExecutableCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        // Clear collections and dispose of any resources
        ExecutableItems.Clear();
        base.OnDisposing();
    }

    #endregion
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