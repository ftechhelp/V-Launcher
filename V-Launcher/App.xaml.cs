using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using V_Launcher.Services;
using V_Launcher.ViewModels;

namespace V_Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private MainViewModel? _mainViewModel;
        private ILogger<App>? _logger;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Set up global exception handlers
                SetupGlobalExceptionHandlers();

                // Configure services
                _host = CreateHostBuilder().Build();
                
                // Start the host
                await _host.StartAsync();

                // Get logger after host is started
                _logger = _host.Services.GetRequiredService<ILogger<App>>();
                _logger.LogInformation("Application starting up");

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
                    // Start minimized to tray - don't show the window at all
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
                    services.AddSingleton<IProcessLauncher, ProcessLauncher>();
                    services.AddSingleton<IStartupRegistryService, StartupRegistryService>();

                    // Register ViewModels with logging support
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<LauncherViewModel>();
                    services.AddTransient<CredentialManagementViewModel>();
                    services.AddTransient<ExecutableManagementViewModel>();
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

        private MessageBoxResult ShowErrorDialog(string title, string message, Exception? exception = null)
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
