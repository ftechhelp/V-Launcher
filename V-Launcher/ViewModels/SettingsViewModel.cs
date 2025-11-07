using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using V_Launcher.Models;
using V_Launcher.Services;

namespace V_Launcher.ViewModels;

/// <summary>
/// ViewModel for managing application settings with MVVM pattern
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IStartupRegistryService _startupRegistryService;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private ApplicationSettings _settings = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    public SettingsViewModel(
        IConfigurationRepository configurationRepository,
        IStartupRegistryService startupRegistryService,
        ILogger<SettingsViewModel> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _startupRegistryService = startupRegistryService ?? throw new ArgumentNullException(nameof(startupRegistryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize commands
        LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, CanSaveSettings);
        ToggleStartOnWindowsStartCommand = new AsyncRelayCommand<bool>(ToggleStartOnWindowsStartAsync, CanToggleSettings);
        ToggleStartMinimizedCommand = new RelayCommand<bool>(ToggleStartMinimized, CanToggleSettings);
        ToggleMinimizeOnCloseCommand = new RelayCommand<bool>(ToggleMinimizeOnClose, CanToggleSettings);
        ResetToDefaultsCommand = new AsyncRelayCommand(ResetToDefaultsAsync, CanResetSettings);

        // Settings will be loaded when the MainViewModel initializes
    }

    #region Commands

    public IAsyncRelayCommand LoadSettingsCommand { get; }
    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IAsyncRelayCommand<bool> ToggleStartOnWindowsStartCommand { get; }
    public IRelayCommand<bool> ToggleStartMinimizedCommand { get; }
    public IRelayCommand<bool> ToggleMinimizeOnCloseCommand { get; }
    public IAsyncRelayCommand ResetToDefaultsCommand { get; }

    #endregion

    #region Settings Management

    /// <summary>
    /// Loads application settings from the configuration repository
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        try
        {
            IsLoading = true;
            ClearStatus();
            _logger.LogDebug("Loading application settings");

            var loadedSettings = await _configurationRepository.LoadSettingsAsync();
            
            // Check if we have existing settings with old defaults (all true)
            if (loadedSettings != null && 
                loadedSettings.StartOnWindowsStart == true && 
                loadedSettings.StartMinimized == true && 
                loadedSettings.MinimizeOnClose == true)
            {
                _logger.LogInformation("Detected old default settings, migrating to new defaults (all false)");
                
                // Migrate to new defaults
                Settings = new ApplicationSettings
                {
                    StartOnWindowsStart = false,
                    StartMinimized = false,
                    MinimizeOnClose = false
                };
                
                // Update registry to match new default
                await _startupRegistryService.SetStartupEnabledAsync(false);
                
                // Save the migrated settings
                await SaveSettingsAsync();
                
                SetStatus("Settings migrated to new defaults (all unchecked)");
                _logger.LogInformation("Settings successfully migrated to new defaults");
            }
            else
            {
                Settings = loadedSettings ?? new ApplicationSettings();
                
                // Sync registry state with loaded settings
                await SyncStartupRegistryStateAsync();
                
                SetStatus("Settings loaded successfully");
                _logger.LogInformation("Application settings loaded successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading application settings");
            SetError($"Failed to load settings: {ex.Message}");
            
            // Use default settings on error
            Settings = new ApplicationSettings();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves application settings to the configuration repository
    /// </summary>
    private async Task SaveSettingsAsync()
    {
        try
        {
            IsSaving = true;
            ClearStatus();
            _logger.LogDebug("Saving application settings");

            if (!ValidateSettings())
            {
                return;
            }

            await _configurationRepository.SaveSettingsAsync(Settings);
            
            SetStatus("Settings saved successfully");
            _logger.LogInformation("Application settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application settings");
            SetError($"Failed to save settings: {ex.Message}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Toggles the Start with Windows setting
    /// </summary>
    private async Task ToggleStartOnWindowsStartAsync(bool enabled)
    {
        try
        {
            ClearStatus();
            _logger.LogDebug("Toggling Start with Windows setting to {Enabled}", enabled);

            var success = await _startupRegistryService.SetStartupEnabledAsync(enabled);
            
            if (success)
            {
                Settings.StartOnWindowsStart = enabled;
                await SaveSettingsAsync();
                
                var status = enabled ? "enabled" : "disabled";
                SetStatus($"Windows startup {status} successfully");
                _logger.LogInformation("Windows startup setting changed to {Enabled}", enabled);
            }
            else
            {
                SetError("Failed to update Windows startup setting. You may need administrator privileges.");
                _logger.LogWarning("Failed to update Windows startup registry setting");
                
                // Revert the UI state if the registry update failed
                OnPropertyChanged(nameof(Settings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Windows startup setting");
            SetError($"Error updating startup setting: {ex.Message}");
            
            // Revert the UI state on error
            OnPropertyChanged(nameof(Settings));
        }
    }

    /// <summary>
    /// Toggles the Start Minimized setting
    /// </summary>
    private void ToggleStartMinimized(bool enabled)
    {
        try
        {
            ClearStatus();
            _logger.LogDebug("Toggling Start Minimized setting to {Enabled}", enabled);

            Settings.StartMinimized = enabled;
            
            // Auto-save the setting
            _ = SaveSettingsAsync();
            
            var status = enabled ? "enabled" : "disabled";
            SetStatus($"Start minimized {status}");
            _logger.LogInformation("Start minimized setting changed to {Enabled}", enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Start Minimized setting");
            SetError($"Error updating start minimized setting: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles the Minimize on Close setting
    /// </summary>
    private void ToggleMinimizeOnClose(bool enabled)
    {
        try
        {
            ClearStatus();
            _logger.LogDebug("Toggling Minimize on Close setting to {Enabled}", enabled);

            Settings.MinimizeOnClose = enabled;
            
            // Auto-save the setting
            _ = SaveSettingsAsync();
            
            var status = enabled ? "enabled" : "disabled";
            SetStatus($"Minimize on close {status}");
            _logger.LogInformation("Minimize on close setting changed to {Enabled}", enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Minimize on Close setting");
            SetError($"Error updating minimize on close setting: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets all settings to their default values
    /// </summary>
    private async Task ResetToDefaultsAsync()
    {
        try
        {
            ClearStatus();
            _logger.LogDebug("Resetting settings to defaults");

            Settings = new ApplicationSettings();
            
            // Update registry to match default startup setting
            await _startupRegistryService.SetStartupEnabledAsync(Settings.StartOnWindowsStart);
            
            await SaveSettingsAsync();
            
            SetStatus("Settings reset to defaults");
            _logger.LogInformation("Settings reset to default values");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings to defaults");
            SetError($"Error resetting settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Forces settings to use new default values (unchecked) - for migration purposes
    /// </summary>
    public async Task ForceResetToNewDefaultsAsync()
    {
        try
        {
            ClearStatus();
            _logger.LogDebug("Forcing reset to new default values");

            // Create new settings with explicit false values
            Settings = new ApplicationSettings
            {
                StartOnWindowsStart = false,
                StartMinimized = false,
                MinimizeOnClose = false
            };
            
            // Update registry to match new default startup setting (false)
            await _startupRegistryService.SetStartupEnabledAsync(false);
            
            await SaveSettingsAsync();
            
            SetStatus("Settings updated to new defaults (all unchecked)");
            _logger.LogInformation("Settings forced to new default values (all false)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing reset to new defaults");
            SetError($"Error updating settings: {ex.Message}");
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the current settings
    /// </summary>
    /// <returns>True if settings are valid, false otherwise</returns>
    private bool ValidateSettings()
    {
        try
        {
            ClearValidation();

            // Settings validation logic
            if (Settings == null)
            {
                SetValidationError("Settings cannot be null");
                return false;
            }

            // All boolean settings are inherently valid, but we could add more complex validation here
            // For example, checking if certain combinations of settings are valid

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during settings validation");
            SetValidationError($"Validation error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets a validation error message
    /// </summary>
    private void SetValidationError(string message)
    {
        ValidationMessage = message;
        SetError(message);
    }

    /// <summary>
    /// Clears validation messages
    /// </summary>
    private void ClearValidation()
    {
        ValidationMessage = string.Empty;
    }

    #endregion

    #region Registry Synchronization

    /// <summary>
    /// Syncs the registry state with the current application settings
    /// </summary>
    private async Task SyncStartupRegistryStateAsync()
    {
        try
        {
            var registryEnabled = await _startupRegistryService.IsStartupEnabledAsync();
            
            // If there's a mismatch, update the registry to match the settings
            if (registryEnabled != Settings.StartOnWindowsStart)
            {
                _logger.LogDebug("Registry state mismatch detected. Registry: {Registry}, Settings: {Settings}", 
                    registryEnabled, Settings.StartOnWindowsStart);
                
                var success = await _startupRegistryService.SetStartupEnabledAsync(Settings.StartOnWindowsStart);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to sync startup registry state with application settings");
                    // Update the setting to match the actual registry state
                    Settings.StartOnWindowsStart = registryEnabled;
                    await SaveSettingsAsync();
                    
                    SetError("Registry sync failed - setting updated to match current state");
                }
                else
                {
                    _logger.LogDebug("Registry state synchronized successfully");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing startup registry state");
            SetError($"Registry sync error: {ex.Message}");
        }
    }

    #endregion

    #region Command Can Execute Methods

    private bool CanSaveSettings() => !IsLoading && !IsSaving && Settings != null;
    private bool CanToggleSettings(bool parameter) => !IsLoading && !IsSaving;
    private bool CanResetSettings() => !IsLoading && !IsSaving;

    #endregion

    #region Status and Error Handling

    /// <summary>
    /// Sets a status message
    /// </summary>
    private void SetStatus(string message)
    {
        StatusMessage = message;
        HasError = false;
        
        // Clear status after a delay
        _ = Task.Delay(3000).ContinueWith(_ => 
        {
            if (!IsDisposed && StatusMessage == message)
            {
                ClearStatus();
            }
        });
    }

    /// <summary>
    /// Sets an error message
    /// </summary>
    private void SetError(string message)
    {
        StatusMessage = message;
        HasError = true;
    }

    /// <summary>
    /// Clears the status message
    /// </summary>
    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        HasError = false;
    }

    #endregion

    #region Property Change Notifications

    partial void OnIsLoadingChanged(bool value)
    {
        // Update command can execute states when loading state changes
        SaveSettingsCommand.NotifyCanExecuteChanged();
        ResetToDefaultsCommand.NotifyCanExecuteChanged();
        ToggleStartOnWindowsStartCommand.NotifyCanExecuteChanged();
        ToggleStartMinimizedCommand.NotifyCanExecuteChanged();
        ToggleMinimizeOnCloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        // Update command can execute states when saving state changes
        SaveSettingsCommand.NotifyCanExecuteChanged();
        ResetToDefaultsCommand.NotifyCanExecuteChanged();
        ToggleStartOnWindowsStartCommand.NotifyCanExecuteChanged();
        ToggleStartMinimizedCommand.NotifyCanExecuteChanged();
        ToggleMinimizeOnCloseCommand.NotifyCanExecuteChanged();
    }

    partial void OnSettingsChanged(ApplicationSettings value)
    {
        // Update command can execute states when settings change
        SaveSettingsCommand.NotifyCanExecuteChanged();
        
        // Clear any previous validation errors when settings change
        ClearValidation();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets a copy of the current settings
    /// </summary>
    /// <returns>A copy of the current ApplicationSettings</returns>
    public ApplicationSettings GetCurrentSettings()
    {
        return Settings?.Clone() ?? new ApplicationSettings();
    }

    /// <summary>
    /// Updates the settings with new values
    /// </summary>
    /// <param name="newSettings">The new settings to apply</param>
    public async Task UpdateSettingsAsync(ApplicationSettings newSettings)
    {
        if (newSettings == null)
            throw new ArgumentNullException(nameof(newSettings));

        try
        {
            _logger.LogDebug("Updating settings programmatically");
            
            Settings = newSettings.Clone();
            
            // Sync registry state if startup setting changed
            await SyncStartupRegistryStateAsync();
            
            await SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings programmatically");
            SetError($"Error updating settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes settings from the repository
    /// </summary>
    public async Task RefreshSettingsAsync()
    {
        await LoadSettingsAsync();
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when settings are successfully saved
    /// </summary>
    public event EventHandler<ApplicationSettings>? SettingsSaved;

    /// <summary>
    /// Event raised when a setting is changed
    /// </summary>
    public event EventHandler<SettingChangedEventArgs>? SettingChanged;

    /// <summary>
    /// Raises the SettingsSaved event
    /// </summary>
    private void OnSettingsSaved()
    {
        SettingsSaved?.Invoke(this, Settings);
    }

    /// <summary>
    /// Raises the SettingChanged event
    /// </summary>
    private void OnSettingChanged(string settingName, object? oldValue, object? newValue)
    {
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(settingName, oldValue, newValue));
    }

    #endregion

    #region Disposal

    protected override void OnDisposing()
    {
        try
        {
            _logger.LogDebug("Disposing SettingsViewModel");
            
            // Clear any pending status messages
            ClearStatus();
            ClearValidation();
            
            _logger.LogDebug("SettingsViewModel disposal completed");
        }
        catch (Exception ex)
        {
            // Last resort error handling
            System.Diagnostics.Debug.WriteLine($"Error during SettingsViewModel disposal: {ex.Message}");
        }
        finally
        {
            base.OnDisposing();
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for setting changed events
/// </summary>
public class SettingChangedEventArgs : EventArgs
{
    public string SettingName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public SettingChangedEventArgs(string settingName, object? oldValue, object? newValue)
    {
        SettingName = settingName ?? throw new ArgumentNullException(nameof(settingName));
        OldValue = oldValue;
        NewValue = newValue;
    }
}