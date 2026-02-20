using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using V_Launcher.Models;
using V_Launcher.Resources;
using V_Launcher.Services;

namespace V_Launcher.ViewModels;

/// <summary>
/// ViewModel for ad hoc operations like password copy and one-off launches.
/// </summary>
public partial class AdHocLauncherViewModel : ViewModelBase
{
    private readonly ICredentialService _credentialService;
    private readonly IExecutableService _executableService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IClipboardService _clipboardService;

    private ADAccount? _selectedClipboardAccount;
    private ADAccount? _selectedLaunchAccount;
    private string _executablePath = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public AdHocLauncherViewModel(
        ICredentialService credentialService,
        IExecutableService executableService,
        IProcessLauncher processLauncher,
        IClipboardService clipboardService)
    {
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _executableService = executableService ?? throw new ArgumentNullException(nameof(executableService));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));

        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);
        CopyPasswordCommand = new AsyncRelayCommand(CopyPasswordAsync, CanExecuteCommands);
        LaunchExecutableCommand = new AsyncRelayCommand(LaunchExecutableAsync, CanExecuteCommands);
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable, CanBrowse);
        BrowseWorkingDirectoryCommand = new RelayCommand(BrowseWorkingDirectory, CanBrowse);
    }

    public IAsyncRelayCommand LoadAccountsCommand { get; }
    public IAsyncRelayCommand CopyPasswordCommand { get; }
    public IAsyncRelayCommand LaunchExecutableCommand { get; }
    public IRelayCommand BrowseExecutableCommand { get; }
    public IRelayCommand BrowseWorkingDirectoryCommand { get; }

    public ObservableCollection<ADAccount> AvailableAccounts { get; } = new();

    public ADAccount? SelectedClipboardAccount
    {
        get => _selectedClipboardAccount;
        set => SetProperty(ref _selectedClipboardAccount, value);
    }

    public ADAccount? SelectedLaunchAccount
    {
        get => _selectedLaunchAccount;
        set => SetProperty(ref _selectedLaunchAccount, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CopyPasswordCommand.NotifyCanExecuteChanged();
                LaunchExecutableCommand.NotifyCanExecuteChanged();
                BrowseExecutableCommand.NotifyCanExecuteChanged();
                BrowseWorkingDirectoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            IsLoading = true;
            ClearStatus();

            var accounts = await _credentialService.GetAccountsAsync();

            await InvokeOnUIThreadAsync(() =>
            {
                AvailableAccounts.Clear();
                foreach (var account in accounts)
                {
                    AvailableAccounts.Add(account);
                }
            });
        }
        catch (Exception ex)
        {
            SetError(string.Format(AdHocResources.AdHocAccountLoadFailedMessage, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CopyPasswordAsync()
    {
        if (SelectedClipboardAccount == null)
        {
            SetError(AdHocResources.AdHocSelectAccountMessage);
            return;
        }

        try
        {
            IsLoading = true;
            ClearStatus();

            var password = await _credentialService.DecryptPasswordAsync(SelectedClipboardAccount);
            InvokeOnUIThread(() => _clipboardService.SetText(password));

            SetStatus(AdHocResources.AdHocPasswordCopiedMessage);
        }
        catch (Exception ex)
        {
            SetError(string.Format(AdHocResources.AdHocCopyPasswordFailedMessage, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LaunchExecutableAsync()
    {
        if (SelectedLaunchAccount == null)
        {
            SetError(AdHocResources.AdHocSelectAccountMessage);
            return;
        }

        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            SetError(AdHocResources.AdHocSelectExecutableMessage);
            return;
        }

        if (!_executableService.ValidateExecutablePath(ExecutablePath))
        {
            SetError(AdHocResources.AdHocExecutableInvalidMessage);
            return;
        }

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
        {
            SetError(AdHocResources.AdHocWorkingDirectoryInvalidMessage);
            return;
        }

        try
        {
            IsLoading = true;
            ClearStatus();

            var password = await _credentialService.DecryptPasswordAsync(SelectedLaunchAccount);
            var config = new ExecutableConfiguration
            {
                DisplayName = Path.GetFileNameWithoutExtension(ExecutablePath.Trim()),
                ExecutablePath = ExecutablePath.Trim(),
                ADAccountId = SelectedLaunchAccount.Id,
                Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim(),
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim()
            };

            await _processLauncher.LaunchAsync(config, SelectedLaunchAccount, password);
            SetStatus(AdHocResources.AdHocLaunchSuccessMessage);
        }
        catch (Exception ex)
        {
            SetError(string.Format(AdHocResources.AdHocLaunchFailedMessage, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BrowseExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = AdHocResources.AdHocBrowseExecutableTitle,
            Filter = AdHocResources.AdHocBrowseExecutableFilter,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ExecutablePath = dialog.FileName;

            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }
    }

    private void BrowseWorkingDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = AdHocResources.AdHocBrowseWorkingDirectoryTitle,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && Directory.Exists(WorkingDirectory))
        {
            dialog.SelectedPath = WorkingDirectory;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WorkingDirectory = dialog.SelectedPath;
        }
    }

    private bool CanExecuteCommands() => !IsLoading;

    private bool CanBrowse() => !IsLoading;

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

}
