using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using V_Launcher.Helpers;
using V_Launcher.Models;
using V_Launcher.Services;
using V_Launcher.Validation;

namespace V_Launcher.ViewModels;

/// <summary>
/// ViewModel for managing AD account credentials with secure password handling
/// </summary>
public partial class CredentialManagementViewModel : ViewModelBase
{
    private readonly ICredentialService _credentialService;
    private readonly Func<Task>? _onDataChanged;

    [ObservableProperty]
    private ObservableCollection<ADAccount> _accounts = new();

    [ObservableProperty]
    private ADAccount? _selectedAccount;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _domain = string.Empty;

    [ObservableProperty]
    private SecureString? _password;

    [ObservableProperty]
    private SecureString? _confirmPassword;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private bool _isLoading;

    public CredentialManagementViewModel(ICredentialService credentialService, Func<Task>? onDataChanged = null)
    {
        _credentialService = credentialService;
        _onDataChanged = onDataChanged;
        
        // Initialize commands
        AddAccountCommand = new AsyncRelayCommand(AddAccountAsync, CanExecuteAccountCommand);
        EditAccountCommand = new AsyncRelayCommand(EditAccountAsync, CanEditAccount);
        SaveAccountCommand = new AsyncRelayCommand(SaveAccountAsync, CanSaveAccount);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync, CanDeleteAccount);
        CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
        LoadAccountsCommand = new AsyncRelayCommand(LoadAccountsAsync);

        // Load accounts on initialization
        _ = LoadAccountsAsync();
    }

    #region Commands

    public IAsyncRelayCommand AddAccountCommand { get; }
    public IAsyncRelayCommand EditAccountCommand { get; }
    public IAsyncRelayCommand SaveAccountCommand { get; }
    public IAsyncRelayCommand DeleteAccountCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand LoadAccountsCommand { get; }

    #endregion

    #region Command Implementations

    private async Task AddAccountAsync()
    {
        ClearForm();
        IsEditing = true;
        await Task.CompletedTask;
    }

    private async Task EditAccountAsync()
    {
        if (SelectedAccount == null) return;

        DisplayName = SelectedAccount.DisplayName;
        Username = SelectedAccount.Username;
        Domain = SelectedAccount.Domain;
        Password = new SecureString();
        ConfirmPassword = new SecureString();
        IsEditing = true;

        await Task.CompletedTask;
    }

    private async Task SaveAccountAsync()
    {
        try
        {
            if (!ValidateInput()) return;

            IsLoading = true;
            ClearValidation();

            var account = SelectedAccount ?? new ADAccount();
            account.DisplayName = DisplayName.Trim();
            account.Username = Username.Trim();
            account.Domain = Domain.Trim();

            // Convert SecureString to plain string for encryption
            var plainPassword = SecureStringHelper.ConvertToString(Password!);
            
            // Validate password confirmation
            if (!SecureStringHelper.AreEqual(Password, ConfirmPassword))
            {
                SetValidationError("Passwords do not match.");
                return;
            }

            var savedAccount = await _credentialService.SaveAccountAsync(account, plainPassword);

            // Update or add to collection
            if (SelectedAccount != null)
            {
                var index = Accounts.IndexOf(SelectedAccount);
                if (index >= 0)
                {
                    Accounts[index] = savedAccount;
                }
            }
            else
            {
                Accounts.Add(savedAccount);
            }

            ClearForm();
            IsEditing = false;
            
            // Notify that data has changed to refresh other views
            if (_onDataChanged != null)
            {
                _ = _onDataChanged();
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to save account: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount == null) return;

        try
        {
            IsLoading = true;
            await _credentialService.DeleteAccountAsync(SelectedAccount.Id);
            Accounts.Remove(SelectedAccount);
            SelectedAccount = null;
            ClearForm();
            
            // Notify that data has changed to refresh other views
            if (_onDataChanged != null)
            {
                _ = _onDataChanged();
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to delete account: {ex.Message}");
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
        SelectedAccount = null;
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            IsLoading = true;
            var accounts = await _credentialService.GetAccountsAsync();
            
            Accounts.Clear();
            foreach (var account in accounts)
            {
                Accounts.Add(account);
            }
        }
        catch (Exception ex)
        {
            SetValidationError($"Failed to load accounts: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Command Can Execute Methods

    private bool CanExecuteAccountCommand() => !IsLoading;

    private bool CanEditAccount() => SelectedAccount != null && !IsLoading;

    private bool CanSaveAccount() => IsEditing && !IsLoading && !string.IsNullOrWhiteSpace(DisplayName) 
                                   && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Domain)
                                   && Password != null && Password.Length > 0;

    private bool CanDeleteAccount() => SelectedAccount != null && !IsLoading;

    #endregion

    #region Validation

    private bool ValidateInput()
    {
        ClearValidation();

        // Validate required fields
        var displayNameResult = ValidationHelper.ValidateRequired(DisplayName, "Display name");
        if (displayNameResult != ValidationResult.Success)
        {
            SetValidationError(displayNameResult!.ErrorMessage!);
            return false;
        }

        var usernameResult = ValidationHelper.ValidateRequired(Username, "Username");
        if (usernameResult != ValidationResult.Success)
        {
            SetValidationError(usernameResult!.ErrorMessage!);
            return false;
        }

        var domainResult = ValidationHelper.ValidateRequired(Domain, "Domain");
        if (domainResult != ValidationResult.Success)
        {
            SetValidationError(domainResult!.ErrorMessage!);
            return false;
        }

        var passwordResult = ValidationHelper.ValidateSecureStringRequired(Password, "Password");
        if (passwordResult != ValidationResult.Success)
        {
            SetValidationError(passwordResult!.ErrorMessage!);
            return false;
        }

        var confirmPasswordResult = ValidationHelper.ValidateSecureStringRequired(ConfirmPassword, "Password confirmation");
        if (confirmPasswordResult != ValidationResult.Success)
        {
            SetValidationError(confirmPasswordResult!.ErrorMessage!);
            return false;
        }

        // Validate field formats
        var usernameFormatResult = ValidationHelper.ValidateUsername(Username, "Username");
        if (usernameFormatResult != ValidationResult.Success)
        {
            SetValidationError(usernameFormatResult!.ErrorMessage!);
            return false;
        }

        var domainFormatResult = ValidationHelper.ValidateDomain(Domain, "Domain");
        if (domainFormatResult != ValidationResult.Success)
        {
            SetValidationError(domainFormatResult!.ErrorMessage!);
            return false;
        }

        // Validate password minimum length
        var passwordLengthResult = ValidationHelper.ValidateSecureStringMinLength(Password, 1, "Password");
        if (passwordLengthResult != ValidationResult.Success)
        {
            SetValidationError(passwordLengthResult!.ErrorMessage!);
            return false;
        }

        // Validate display name uniqueness
        var existingAccount = Accounts.FirstOrDefault(a => 
            a.DisplayName.Equals(DisplayName.Trim(), StringComparison.OrdinalIgnoreCase) 
            && (SelectedAccount == null || a.Id != SelectedAccount.Id));
        
        if (existingAccount != null)
        {
            SetValidationError("An account with this display name already exists.");
            return false;
        }

        // Validate username/domain combination uniqueness
        var existingUserAccount = Accounts.FirstOrDefault(a => 
            a.Username.Equals(Username.Trim(), StringComparison.OrdinalIgnoreCase) 
            && a.Domain.Equals(Domain.Trim(), StringComparison.OrdinalIgnoreCase)
            && (SelectedAccount == null || a.Id != SelectedAccount.Id));
        
        if (existingUserAccount != null)
        {
            SetValidationError("An account with this username and domain already exists.");
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

    #endregion

    #region Helper Methods

    private void ClearForm()
    {
        DisplayName = string.Empty;
        Username = string.Empty;
        Domain = string.Empty;
        Password?.Dispose();
        Password = null;
        ConfirmPassword?.Dispose();
        ConfirmPassword = null;
        ClearValidation();
    }



    #endregion

    #region Property Change Notifications

    partial void OnSelectedAccountChanged(ADAccount? value)
    {
        // Update command can execute states
        EditAccountCommand.NotifyCanExecuteChanged();
        DeleteAccountCommand.NotifyCanExecuteChanged();
    }

    partial void OnDisplayNameChanged(string value)
    {
        SaveAccountCommand.NotifyCanExecuteChanged();
        if (HasValidationError && !string.IsNullOrWhiteSpace(value))
        {
            ClearValidation();
        }
    }

    partial void OnUsernameChanged(string value)
    {
        SaveAccountCommand.NotifyCanExecuteChanged();
        if (HasValidationError && !string.IsNullOrWhiteSpace(value))
        {
            ClearValidation();
        }
    }

    partial void OnDomainChanged(string value)
    {
        SaveAccountCommand.NotifyCanExecuteChanged();
        if (HasValidationError && !string.IsNullOrWhiteSpace(value))
        {
            ClearValidation();
        }
    }

    partial void OnPasswordChanged(SecureString? value)
    {
        SaveAccountCommand.NotifyCanExecuteChanged();
        if (HasValidationError && value != null && value.Length > 0)
        {
            ClearValidation();
        }
    }

    partial void OnIsLoadingChanged(bool value)
    {
        // Update all command can execute states when loading state changes
        AddAccountCommand.NotifyCanExecuteChanged();
        EditAccountCommand.NotifyCanExecuteChanged();
        SaveAccountCommand.NotifyCanExecuteChanged();
        DeleteAccountCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        // Dispose of SecureString instances
        Password?.Dispose();
        ConfirmPassword?.Dispose();
        
        base.OnDisposing();
    }

    #endregion
}