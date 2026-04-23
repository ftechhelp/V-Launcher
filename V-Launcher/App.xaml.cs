using System.Windows;
using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AppResources = V_Launcher.Properties.Resources;
using V_Launcher.Services;
using V_Launcher.ViewModels;
using V_Launcher.Views;

namespace V_Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private MainViewModel? _mainViewModel;
        private ITotpService? _totpService;
        private ILogger<App>? _logger;
        private bool _restoreWindowAfterUnlock;
        private bool _isSessionReauthenticationInProgress;
        private bool _sessionSwitchSubscribed;
        private const string ResetOtpArgument = "--reset-otp";

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Prevent WPF from shutting down while auth dialogs are closing
                // before the main window is fully established.
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Set up global exception handlers
                SetupGlobalExceptionHandlers();

                // Configure services
                _host = CreateHostBuilder().Build();
                
                // Start the host
                await _host.StartAsync();

                // Get logger after host is started
                _logger = _host.Services.GetRequiredService<ILogger<App>>();
                _logger.LogInformation("Application starting up");

                _totpService = _host.Services.GetRequiredService<ITotpService>();

                if (HasResetOtpArgument(e.Args))
                {
                    await ResetOtpAsync();
                    Shutdown(0);
                    return;
                }

                // Perform mandatory OTP authentication
                await _totpService.LoadConfigurationAsync();

                if (!_totpService.IsOtpConfigured)
                {
                    // First launch: force OTP setup before allowing access
                    _logger.LogInformation("OTP not configured, requiring initial setup");
                    var setupWindow = new OtpSetupWindow(_totpService);
                    bool? setupResult = setupWindow.ShowDialog();

                    if (setupResult != true || !setupWindow.SetupCompleted)
                    {
                        _logger.LogWarning("OTP setup was not completed, shutting down");
                        Shutdown(1);
                        return;
                    }

                    _logger.LogInformation("OTP setup completed on first launch");
                }

                // Always require OTP verification at launch
                _logger.LogInformation("Prompting for OTP verification");
                if (!ShowOtpVerificationDialog())
                {
                    _logger.LogWarning("OTP verification failed or was cancelled, shutting down");
                    Shutdown(1);
                    return;
                }

                _logger.LogInformation("OTP verification successful");

                // Get the main ViewModel
                _mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

                // Create the main window
                var mainWindow = new MainWindow
                {
                    DataContext = _mainViewModel
                };

                // Handle application startup in the ViewModel
                await _mainViewModel.HandleApplicationStartupAsync();

                // Check if we should start minimized
                if (_mainViewModel.SettingsViewModel.Settings.StartMinimized)
                {
                    // Start minimized to tray - show once so WPF tracks the main window lifecycle,
                    // then immediately minimize to tray.
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Minimized;
                    mainWindow.ShowInTaskbar = false;
                    // Call MinimizeToTrayExternal to properly set up the tray icon
                    mainWindow.MinimizeToTrayExternal();
                }
                else
                {
                    mainWindow.Show();
                }
                
                MainWindow = mainWindow;
                SystemEvents.SessionSwitch += OnSessionSwitch;
                _sessionSwitchSubscribed = true;

                // Main window lifecycle now controls application shutdown.
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                _logger.LogInformation("Application startup completed successfully");
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Critical error during application startup");
                
                ShowCriticalErrorDialog(
                    "Application Startup Failed",
                    $"The application failed to start due to a critical error:\n\n{ex.Message}\n\nThe application will now close.",
                    ex);
                
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation("Application shutting down");

                if (_sessionSwitchSubscribed)
                {
                    SystemEvents.SessionSwitch -= OnSessionSwitch;
                    _sessionSwitchSubscribed = false;
                }

                // Handle application shutdown in the ViewModel
                if (_mainViewModel != null)
                {
                    await _mainViewModel.HandleApplicationShutdownAsync();
                    _mainViewModel.Dispose();
                }

                // Stop and dispose the host
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }

                _logger?.LogInformation("Application shutdown completed successfully");
            }
            catch (Exception ex)
            {
                // Log shutdown errors but don't prevent shutdown
                _logger?.LogError(ex, "Error during application shutdown");
                System.Diagnostics.Debug.WriteLine($"Error during application shutdown: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private static IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureServices((context, services) =>
                {
                    // Register services with proper error handling
                    services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
                    services.AddSingleton<ICredentialService, CredentialService>();
                    services.AddSingleton<IExecutableService, ExecutableService>();
                    services.AddSingleton<INetworkDriveService, NetworkDriveService>();
                    services.AddSingleton<IClipboardService, ClipboardService>();
                    services.AddSingleton<IProcessLauncher, ProcessLauncher>();
                    services.AddSingleton<IStartupRegistryService, StartupRegistryService>();
                    services.AddSingleton<ITotpService, TotpService>();
                    services.AddSingleton<IApplicationUpdateService>(provider =>
                    {
                        var logger = provider.GetRequiredService<ILogger<ApplicationUpdateService>>();
                        return new ApplicationUpdateService(new HttpClient(), logger);
                    });

                    // Register ViewModels with logging support
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<LauncherViewModel>();
                    services.AddTransient<CredentialManagementViewModel>();
                    services.AddTransient<ExecutableManagementViewModel>();
                    services.AddTransient<NetworkDriveManagementViewModel>();
                });
        }

        private void SetupGlobalExceptionHandlers()
        {
            // Handle unhandled exceptions in the UI thread
            DispatcherUnhandledException += (sender, e) =>
            {
                _logger?.LogError(e.Exception, "Unhandled exception in UI thread");
                
                var result = ShowErrorDialog(
                    "Unexpected Error",
                    $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nWould you like to continue running the application?",
                    e.Exception);

                if (result == MessageBoxResult.Yes)
                {
                    e.Handled = true; // Continue running
                }
                else
                {
                    Shutdown(1); // Close application
                }
            };

            // Handle unhandled exceptions in background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                _logger?.LogCritical(exception, "Unhandled exception in background thread");
                
                if (e.IsTerminating)
                {
                    ShowCriticalErrorDialog(
                        "Critical Application Error",
                        $"A critical error occurred that will cause the application to terminate:\n\n{exception?.Message ?? "Unknown error"}",
                        exception);
                }
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                _logger?.LogError(e.Exception, "Unobserved task exception");
                e.SetObserved(); // Prevent the process from terminating
            };
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnSessionSwitch(sender, e)));
                return;
            }

            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    HandleSessionLocked();
                    break;
                case SessionSwitchReason.SessionUnlock:
                    HandleSessionUnlocked();
                    break;
            }
        }

        private void HandleSessionLocked()
        {
            if (this.MainWindow is not MainWindow mainWindow)
            {
                return;
            }

            _restoreWindowAfterUnlock = mainWindow.IsVisible && mainWindow.ShowInTaskbar;

            if (mainWindow.IsVisible)
            {
                _logger?.LogInformation("Windows session locked, hiding the application until OTP is verified again");
                mainWindow.MinimizeToTrayExternal(showNotification: false);
            }
        }

        private async void HandleSessionUnlocked()
        {
            try
            {
                await RequireOtpAfterUnlockAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to re-authenticate after Windows session unlock");
                Shutdown(1);
            }
        }

        private Task RequireOtpAfterUnlockAsync()
        {
            if (_totpService is null || !_totpService.IsOtpConfigured || _isSessionReauthenticationInProgress)
            {
                return Task.CompletedTask;
            }

            _isSessionReauthenticationInProgress = true;

            try
            {
                _logger?.LogInformation("Windows session unlocked, requiring OTP re-authentication");

                if (!ShowOtpVerificationDialog())
                {
                    _logger?.LogWarning("OTP re-authentication failed or was cancelled after unlock, shutting down");
                    Shutdown(1);
                    return Task.CompletedTask;
                }

                if (_restoreWindowAfterUnlock && this.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ShowWindow();
                }

                _restoreWindowAfterUnlock = false;
                return Task.CompletedTask;
            }
            finally
            {
                _isSessionReauthenticationInProgress = false;
            }
        }

        private bool ShowOtpVerificationDialog()
        {
            if (_totpService is null)
            {
                throw new InvalidOperationException("OTP service is not initialized.");
            }

            var verificationWindow = new OtpVerificationWindow(_totpService);
            bool? verifyResult = verificationWindow.ShowDialog();

            return verifyResult == true && verificationWindow.IsVerified;
        }

        private async Task ResetOtpAsync()
        {
            if (_totpService is null)
            {
                throw new InvalidOperationException("OTP service is not initialized.");
            }

            await _totpService.ResetOtpAsync();

            System.Windows.MessageBox.Show(
                GetResourceString("OtpResetSuccessMessage", "OTP has been reset. Start the app again to register a new authenticator entry."),
                GetResourceString("OtpResetTitle", "OTP Reset"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private static bool HasResetOtpArgument(IEnumerable<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);

            return args.Any(argument => string.Equals(argument, ResetOtpArgument, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetResourceString(string resourceName, string fallback)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
            ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

            return AppResources.ResourceManager.GetString(resourceName, AppResources.Culture) ?? fallback;
        }

        private System.Windows.MessageBoxResult ShowErrorDialog(string title, string message, Exception? exception = null)
        {
            var detailedMessage = message;
            if (exception != null && !string.IsNullOrEmpty(exception.StackTrace))
            {
                detailedMessage += $"\n\nTechnical Details:\n{exception.GetType().Name}: {exception.Message}";
            }

            return System.Windows.MessageBox.Show(
                detailedMessage,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Error,
                MessageBoxResult.No);
        }

        private void ShowCriticalErrorDialog(string title, string message, Exception? exception = null)
        {
            var detailedMessage = message;
            if (exception != null && !string.IsNullOrEmpty(exception.StackTrace))
            {
                detailedMessage += $"\n\nTechnical Details:\n{exception.GetType().Name}: {exception.Message}";
            }

            System.Windows.MessageBox.Show(
                detailedMessage,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
