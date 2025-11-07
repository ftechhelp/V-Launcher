using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using V_Launcher.Services;
using V_Launcher.ViewModels;

namespace V_LauncherTests;

/// <summary>
/// Tests for application-wide error handling and resilience
/// </summary>
public class ErrorHandlingTests : IDisposable
{
    private readonly IHost _host;
    private readonly IServiceProvider _services;

    public ErrorHandlingTests()
    {
        _host = CreateTestHost();
        _services = _host.Services;
    }

    private IHost CreateTestHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices((context, services) =>
            {
                // Register services
                services.AddSingleton<IConfigurationRepository>(provider => 
                    new ConfigurationRepository("invalid-path-that-will-cause-errors"));
                services.AddSingleton<ICredentialService, CredentialService>();
                services.AddSingleton<IExecutableService, ExecutableService>();
                services.AddSingleton<IProcessLauncher, ProcessLauncher>();
                services.AddSingleton<IStartupRegistryService, StartupRegistryService>();

                // Register ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LauncherViewModel>();
                services.AddTransient<CredentialManagementViewModel>();
                services.AddTransient<ExecutableManagementViewModel>();
            })
            .Build();
    }

    [Fact]
    public async Task MainViewModel_InitializationWithErrors_DoesNotCrash()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();

        // Act - This should not throw even if individual components fail
        await mainViewModel.HandleApplicationStartupAsync();

        // Assert - Application should still be in a usable state
        Assert.False(mainViewModel.IsInitializing);
        Assert.NotNull(mainViewModel.CurrentViewModel);
        
        // The application should handle errors gracefully and continue running
        Assert.True(mainViewModel.HasError || !string.IsNullOrEmpty(mainViewModel.StatusMessage));
    }

    [Fact]
    public async Task MainViewModel_RefreshWithErrors_DoesNotCrash()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.HandleApplicationStartupAsync();

        // Act - This should not throw even if refresh operations fail
        await mainViewModel.RefreshDataAfterChangesAsync();

        // Assert - Application should still be responsive
        Assert.False(mainViewModel.IsInitializing);
        Assert.NotNull(mainViewModel.CurrentViewModel);
    }

    [Fact]
    public async Task MainViewModel_ShutdownWithErrors_DoesNotCrash()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.HandleApplicationStartupAsync();

        // Act - This should not throw even if shutdown operations fail
        await mainViewModel.HandleApplicationShutdownAsync();

        // Assert - Should complete without throwing
        Assert.True(true); // If we get here, shutdown completed successfully
    }

    [Fact]
    public void MainViewModel_Disposal_DoesNotCrash()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();

        // Act - This should not throw even if disposal operations fail
        var exception = Record.Exception(() => mainViewModel.Dispose());

        // Assert - Disposal should complete without throwing
        Assert.Null(exception);
    }

    [Fact]
    public async Task ViewModelNavigation_WithErrors_StillWorks()
    {
        // Arrange
        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        await mainViewModel.HandleApplicationStartupAsync();

        // Act & Assert - Navigation should work even if underlying data has errors
        mainViewModel.ShowCredentialManagementViewCommand.Execute(null);
        Assert.Equal(mainViewModel.CredentialManagementViewModel, mainViewModel.CurrentViewModel);

        mainViewModel.ShowExecutableManagementViewCommand.Execute(null);
        Assert.Equal(mainViewModel.ExecutableManagementViewModel, mainViewModel.CurrentViewModel);

        mainViewModel.ShowLauncherViewCommand.Execute(null);
        Assert.Equal(mainViewModel.LauncherViewModel, mainViewModel.CurrentViewModel);
    }

    public void Dispose()
    {
        _host?.Dispose();
    }
}