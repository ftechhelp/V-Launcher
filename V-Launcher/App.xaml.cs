using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Configure services
                _host = CreateHostBuilder().Build();
                
                // Start the host
                await _host.StartAsync();

                // Get the main ViewModel
                _mainViewModel = _host.Services.GetRequiredService<MainViewModel>();

                // Create and show the main window
                var mainWindow = new MainWindow
                {
                    DataContext = _mainViewModel
                };

                // Handle application startup in the ViewModel
                await _mainViewModel.HandleApplicationStartupAsync();

                mainWindow.Show();
                MainWindow = mainWindow;

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to start application: {ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                Shutdown(1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                // Log shutdown errors but don't prevent shutdown
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
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddSingleton<IConfigurationRepository, ConfigurationRepository>();
                    services.AddSingleton<ICredentialService, CredentialService>();
                    services.AddSingleton<IExecutableService, ExecutableService>();
                    services.AddSingleton<IProcessLauncher, ProcessLauncher>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<LauncherViewModel>();
                    services.AddTransient<CredentialManagementViewModel>();
                    services.AddTransient<ExecutableManagementViewModel>();
                });
        }
    }
}
