using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using V_Launcher.Models;
using V_Launcher.Services;
using V_Launcher.Validation;

namespace V_Launcher.ViewModels;

public partial class ExecutableManagementViewModel : ViewModelBase
{
    private readonly IExecutableService _executableService;
    private readonly ICredentialService _credentialService;
    private readonly Func<Task>? _onDataChanged;

    [ObservableProperty]
    private ObservableCollection<ExecutableConfiguration> _configurations = new();

    [ObservableProperty]
    private ObservableCollection<ADAccount> _availableAccounts = new();

    [ObservableProperty]
    private ExecutableConfiguration? _selectedConfiguration;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _customIconPath = string.Empty;

    [ObservableProperty]
    private ADAccount? _selectedAccount;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private BitmapImage? _previewIcon;

    public ExecutableManagementViewModel(IExecutableService executableService, ICredentialService credentialService, Func<Task>? onDataChanged = null)
    {
        _executableService = executableService;
        _credentialService = credentialService;
        _onDataChanged = onDataChanged;
        
        AddConfigurationCommand = new AsyncRelayCommand(AddConfigurationAsync, CanExecuteConfigurationCommand);
        EditConfigurationCommand = new AsyncRelayCommand(EditConfigurationAsync, CanEditConfiguration);
        SaveConfigurationCommand = new AsyncRelayCommand(SaveConfigurationAsync, CanSaveConfiguration);
        DeleteConfigurationCommand = new AsyncRelayCommand(DeleteConfigurationAsync, CanDeleteConfiguration);
        CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
        LoadConfigurationsCommand = new AsyncRelayCommand(LoadConfigurationsAsync);
        BrowseExecutableCommand = new RelayCommand(BrowseExecutable, CanBrowseFiles);
        BrowseCustomIconCommand = new RelayCommand(BrowseCustomIcon, CanBrowseFiles);
        ClearCustomIconCommand = new RelayCommand(ClearCustomIcon, () => !string.IsNullOrEmpty(CustomIconPath));
        BrowseWorkingDirectoryCommand = new RelayCommand(BrowseWorkingDirectory, CanBrowseFiles);

        // Don't call async methods in constructor - let MainViewModel handle initialization
    }

    public IAsyncRelayCommand AddConfigurationCommand { get; }
    public IAsyncRelayCommand EditConfigurationCommand { get; }
    public IAsyncRelayCommand SaveConfigurationCommand { get; }
    public IAsyncRelayCommand DeleteConfigurationCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand LoadConfigurationsCommand { get; }
    public IRelayCommand BrowseExecutableCommand { get; }
    public IRelayCommand BrowseCustomIconCommand { get; }
    public IRelayCommand ClearCustomIconCommand { get; }
    public IRelayCommand BrowseWorkingDirectoryCommand { get; }

    private async Task AddConfigurationAsync()
    {
        ClearForm();
        IsEditing = true;
        await Task.CompletedTask;
    }

    private async Task EditConfigurationAsync()
    {
        if (SelectedConfiguration == null) return;

        DisplayName = SelectedConfiguration.DisplayName;
        ExecutablePath = SelectedConfiguration.ExecutablePath;
        CustomIconPath = SelectedConfiguration.CustomIconPath ?? string.Empty;
        Arguments = SelectedConfiguration.Arguments ?? string.Empty;
        WorkingDirectory = SelectedConfiguration.WorkingDirectory ?? string.Empty;
        SelectedAccount = AvailableAccounts.FirstOrDefault(a => a.Id == SelectedConfiguration.ADAccountId);
        
        IsEditing = true;
        await UpdatePreviewIconAsync();
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            if (!ValidateInput()) return;

            IsLoading = true;
            ClearValidation();

            var config = SelectedConfiguration ?? new ExecutableConfiguration();
            config.DisplayName = DisplayName.Trim();
            config.ExecutablePath = ExecutablePath.Trim();
            config.CustomIconPath = string.IsNullOrWhiteSpace(CustomIconPath) ? null : CustomIconPath.Trim();
            config.ADAccountId = SelectedAccount!.Id;
            config.Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments.Trim();
            config.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim();

            var savedConfig = await _executableService.SaveConfigurationAsync(config);

            // Update or add to collection on UI thread
            await InvokeOnUIThreadAsync(() =>
            {
                if (SelectedConfiguration != null)
                {
                    var index = Configurations.IndexOf(SelectedConfiguration);
                    if (index >= 0)
                    {
                        Configurations[index] = savedConfig;
                    }
                }
                else
                {
                    Configurations.Add(savedConfig);
                }
            });

            ClearForm();
            IsEditing = false;
            
            // Notify that data has changed to refresh other views
            if (_onDataChanged != null)
            {
                _ = InvokeOnUIThreadAsync(async () => await _onDataChanged());
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to save configuration: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteConfigurationAsync()
    {
        if (SelectedConfiguration == null) return;

        try
        {
            IsLoading = true;
            await _executableService.DeleteConfigurationAsync(SelectedConfiguration.Id);
            
            // Update collection on UI thread
            var configToRemove = SelectedConfiguration;
            await InvokeOnUIThreadAsync(() =>
            {
                Configurations.Remove(configToRemove);
            });
            
            SelectedConfiguration = null;
            ClearForm();
            
            // Notify that data has changed to refresh other views
            if (_onDataChanged != null)
            {
                _ = InvokeOnUIThreadAsync(async () => await _onDataChanged());
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to delete configuration: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelEdit()
    {
        ClearForm();
        IsEditing = false;
        SelectedConfiguration = null;
    }

    private async Task LoadConfigurationsAsync()
    {
        try
        {
            IsLoading = true;
            var configs = await _executableService.GetConfigurationsAsync();
            
            // Update collections on UI thread
            await InvokeOnUIThreadAsync(() =>
            {
                Configurations.Clear();
                foreach (var config in configs)
                {
                    Configurations.Add(config);
                }
            });
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to load configurations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            var accounts = await _credentialService.GetAccountsAsync();
            
            // Update collections on UI thread
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
            SetValidationError($"Failed to load accounts: {ex.Message}");
        }
    }

    private async Task LoadDataAsync()
    {
        await LoadAccountsAsync();
        await LoadConfigurationsAsync();
    }
    
    /// <summary>
    /// Refreshes available accounts - called when switching to this view
    /// </summary>
    public async Task RefreshAvailableAccountsAsync()
    {
        await LoadAccountsAsync();
    }

    /// <summary>
    /// Public method to initialize data loading - called by MainViewModel
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    private void BrowseExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Executable",
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ExecutablePath = dialog.FileName;
            
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
            
            if (string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
            
            _ = UpdatePreviewIconAsync();
        }
    }

    private void BrowseCustomIcon()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Custom Icon",
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|Icon Files (*.ico)|*.ico|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            CustomIconPath = dialog.FileName;
            _ = UpdatePreviewIconAsync();
        }
    }

    private void ClearCustomIcon()
    {
        CustomIconPath = string.Empty;
        _ = UpdatePreviewIconAsync();
    }

    private void BrowseWorkingDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Working Directory",
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

    private bool CanExecuteConfigurationCommand() => !IsLoading;

    private bool CanEditConfiguration() => SelectedConfiguration != null && !IsLoading;

    private bool CanSaveConfiguration() => IsEditing && !IsLoading && !string.IsNullOrWhiteSpace(DisplayName) 
                                         && !string.IsNullOrWhiteSpace(ExecutablePath) && SelectedAccount != null;

    private bool CanDeleteConfiguration() => SelectedConfiguration != null && !IsLoading;

    private bool CanBrowseFiles() => IsEditing && !IsLoading;

    private bool ValidateInput()
    {
        ClearValidation();

        var displayNameResult = ValidationHelper.ValidateRequired(DisplayName, "Display name");
        if (displayNameResult != ValidationResult.Success)
        {
            SetValidationError(displayNameResult!.ErrorMessage!);
            return false;
        }

        var executablePathResult = ValidationHelper.ValidateRequired(ExecutablePath, "Executable path");
        if (executablePathResult != ValidationResult.Success)
        {
            SetValidationError(executablePathResult!.ErrorMessage!);
            return false;
        }

        if (!_executableService.ValidateExecutablePath(ExecutablePath))
        {
            SetValidationError("The specified executable file does not exist or is not accessible.");
            return false;
        }

        if (SelectedAccount == null)
        {
            SetValidationError("Please select an AD account for this configuration.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CustomIconPath) && !File.Exists(CustomIconPath))
        {
            SetValidationError("The specified custom icon file does not exist.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
        {
            SetValidationError("The specified working directory does not exist.");
            return false;
        }

        return ValidateUniqueness();
    }

    private bool ValidateUniqueness()
    {
        var existingConfig = Configurations.FirstOrDefault(c => 
            c.DisplayName.Equals(DisplayName.Trim(), StringComparison.OrdinalIgnoreCase) 
            && (SelectedConfiguration == null || c.Id != SelectedConfiguration.Id));
        
        if (existingConfig != null)
        {
            SetValidationError("A configuration with this display name already exists.");
            return false;
        }

        var existingPathConfig = Configurations.FirstOrDefault(c => 
            c.ExecutablePath.Equals(ExecutablePath.Trim(), StringComparison.OrdinalIgnoreCase) 
            && c.ADAccountId == SelectedAccount!.Id
            && (SelectedConfiguration == null || c.Id != SelectedConfiguration.Id));
        
        if (existingPathConfig != null)
        {
            SetValidationError("A configuration for this executable with the same AD account already exists.");
            return false;
        }

        return true;
    }

    private void SetValidationError(string message)
    {
        ValidationMessage = message;
        HasValidationError = true;
    }

    private void ClearValidation()
    {
        ValidationMessage = string.Empty;
        HasValidationError = false;
    }

    private void ClearForm()
    {
        DisplayName = string.Empty;
        ExecutablePath = string.Empty;
        CustomIconPath = string.Empty;
        Arguments = string.Empty;
        WorkingDirectory = string.Empty;
        SelectedAccount = null;
        PreviewIcon = null;
        ClearValidation();
    }

    private async Task UpdatePreviewIconAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(ExecutablePath) || !string.IsNullOrWhiteSpace(CustomIconPath))
            {
                var tempConfig = new ExecutableConfiguration
                {
                    ExecutablePath = ExecutablePath,
                    CustomIconPath = string.IsNullOrWhiteSpace(CustomIconPath) ? null : CustomIconPath
                };
                
                PreviewIcon = await _executableService.GetIconAsync(tempConfig);
            }
            else
            {
                PreviewIcon = null;
            }
        }
        catch
        {
            PreviewIcon = null;
        }
    }

    partial void OnSelectedConfigurationChanged(ExecutableConfiguration? value)
    {
        EditConfigurationCommand.NotifyCanExecuteChanged();
        DeleteConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnDisplayNameChanged(string value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        if (HasValidationError && !string.IsNullOrWhiteSpace(value))
        {
            ClearValidation();
        }
    }

    partial void OnExecutablePathChanged(string value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        if (HasValidationError && !string.IsNullOrWhiteSpace(value))
        {
            ClearValidation();
        }
        _ = UpdatePreviewIconAsync();
    }

    partial void OnCustomIconPathChanged(string value)
    {
        if (HasValidationError && (string.IsNullOrWhiteSpace(value) || File.Exists(value)))
        {
            ClearValidation();
        }
        ClearCustomIconCommand.NotifyCanExecuteChanged();
        _ = UpdatePreviewIconAsync();
    }

    partial void OnSelectedAccountChanged(ADAccount? value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        if (HasValidationError && value != null)
        {
            ClearValidation();
        }
    }

    partial void OnWorkingDirectoryChanged(string value)
    {
        if (HasValidationError && (string.IsNullOrWhiteSpace(value) || Directory.Exists(value)))
        {
            ClearValidation();
        }
    }

    partial void OnIsEditingChanged(bool value)
    {
        BrowseExecutableCommand.NotifyCanExecuteChanged();
        BrowseCustomIconCommand.NotifyCanExecuteChanged();
        BrowseWorkingDirectoryCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        AddConfigurationCommand.NotifyCanExecuteChanged();
        EditConfigurationCommand.NotifyCanExecuteChanged();
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        DeleteConfigurationCommand.NotifyCanExecuteChanged();
        BrowseExecutableCommand.NotifyCanExecuteChanged();
        BrowseCustomIconCommand.NotifyCanExecuteChanged();
        BrowseWorkingDirectoryCommand.NotifyCanExecuteChanged();
    }

    protected override void OnDisposing()
    {
        PreviewIcon = null;
        base.OnDisposing();
    }
}