using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V_Launcher.Models;
using V_Launcher.Services;

namespace V_Launcher.ViewModels;

public partial class NetworkDriveManagementViewModel : ViewModelBase
{
    private readonly INetworkDriveService _networkDriveService;
    private readonly ICredentialService _credentialService;
    private readonly Func<Task>? _onDataChanged;

    [ObservableProperty]
    private ObservableCollection<NetworkDriveConfiguration> _configurations = new();

    [ObservableProperty]
    private ObservableCollection<ADAccount> _availableAccounts = new();

    [ObservableProperty]
    private NetworkDriveConfiguration? _selectedConfiguration;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _remotePath = string.Empty;

    [ObservableProperty]
    private ADAccount? _selectedAccount;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasValidationError;

    public NetworkDriveManagementViewModel(INetworkDriveService networkDriveService, ICredentialService credentialService, Func<Task>? onDataChanged = null)
    {
        _networkDriveService = networkDriveService;
        _credentialService = credentialService;
        _onDataChanged = onDataChanged;

        AddConfigurationCommand = new AsyncRelayCommand(AddConfigurationAsync, () => !IsLoading);
        EditConfigurationCommand = new AsyncRelayCommand(EditConfigurationAsync, () => SelectedConfiguration != null && !IsLoading);
        SaveConfigurationCommand = new AsyncRelayCommand(SaveConfigurationAsync, () => IsEditing && !IsLoading && SelectedAccount != null && !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(RemotePath));
        DeleteConfigurationCommand = new AsyncRelayCommand(DeleteConfigurationAsync, () => SelectedConfiguration != null && !IsLoading);
        CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
        LoadConfigurationsCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    public IAsyncRelayCommand AddConfigurationCommand { get; }
    public IAsyncRelayCommand EditConfigurationCommand { get; }
    public IAsyncRelayCommand SaveConfigurationCommand { get; }
    public IAsyncRelayCommand DeleteConfigurationCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand LoadConfigurationsCommand { get; }

    private async Task AddConfigurationAsync()
    {
        ClearForm();
        IsEditing = true;
        await Task.CompletedTask;
    }

    private async Task EditConfigurationAsync()
    {
        if (SelectedConfiguration == null)
        {
            return;
        }

        DisplayName = SelectedConfiguration.DisplayName;
        RemotePath = SelectedConfiguration.RemotePath;
        SelectedAccount = AvailableAccounts.FirstOrDefault(item => item.Id == SelectedConfiguration.ADAccountId);
        IsEditing = true;
        await Task.CompletedTask;
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            if (!ValidateInput())
            {
                return;
            }

            IsLoading = true;
            ClearValidation();

            var configuration = new NetworkDriveConfiguration
            {
                Id = SelectedConfiguration?.Id ?? Guid.NewGuid(),
                DisplayName = DisplayName.Trim(),
                RemotePath = RemotePath.Trim(),
                ADAccountId = SelectedAccount!.Id
            };

            var saved = await _networkDriveService.SaveConfigurationAsync(configuration);

            await InvokeOnUIThreadAsync(() =>
            {
                if (SelectedConfiguration != null)
                {
                    var index = Configurations.IndexOf(SelectedConfiguration);
                    if (index >= 0)
                    {
                        Configurations[index] = saved;
                    }
                }
                else
                {
                    Configurations.Add(saved);
                }
            });

            ClearForm();
            IsEditing = false;

            if (_onDataChanged != null)
            {
                await _onDataChanged();
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to save network drive configuration: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteConfigurationAsync()
    {
        if (SelectedConfiguration == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _networkDriveService.DeleteConfigurationAsync(SelectedConfiguration.Id);

            var toRemove = SelectedConfiguration;
            await InvokeOnUIThreadAsync(() => Configurations.Remove(toRemove));

            ClearForm();

            if (_onDataChanged != null)
            {
                await _onDataChanged();
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to delete network drive configuration: {ex.Message}");
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

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            ClearValidation();

            var accounts = await _credentialService.GetAccountsAsync();
            var configurations = await _networkDriveService.GetConfigurationsAsync();

            await InvokeOnUIThreadAsync(() =>
            {
                AvailableAccounts.Clear();
                foreach (var account in accounts)
                {
                    AvailableAccounts.Add(account);
                }

                Configurations.Clear();
                foreach (var configuration in configurations)
                {
                    Configurations.Add(configuration);
                }
            });
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to load network drive data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool ValidateInput()
    {
        ClearValidation();

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            SetValidationError("Display name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemotePath))
        {
            SetValidationError("Remote path is required.");
            return false;
        }

        if (!RemotePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            SetValidationError("Remote path must be a UNC path (for example: \\server\\share).");
            return false;
        }

        if (SelectedAccount == null)
        {
            SetValidationError("Please select an AD account.");
            return false;
        }

        return true;
    }

    private void ClearForm()
    {
        DisplayName = string.Empty;
        RemotePath = string.Empty;
        SelectedAccount = null;
        SelectedConfiguration = null;
        ClearValidation();
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

    partial void OnIsLoadingChanged(bool value)
    {
        AddConfigurationCommand.NotifyCanExecuteChanged();
        EditConfigurationCommand.NotifyCanExecuteChanged();
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        DeleteConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedConfigurationChanged(NetworkDriveConfiguration? value)
    {
        EditConfigurationCommand.NotifyCanExecuteChanged();
        DeleteConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsEditingChanged(bool value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
        CancelEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnDisplayNameChanged(string value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnRemotePathChanged(string value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAccountChanged(ADAccount? value)
    {
        SaveConfigurationCommand.NotifyCanExecuteChanged();
    }

    protected override void OnDisposing()
    {
        Configurations.Clear();
        AvailableAccounts.Clear();
        base.OnDisposing();
    }
}
