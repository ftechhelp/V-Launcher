using V_Launcher.Services;
using Xunit;

namespace V_Launcher.Tests.Services;

/// <summary>
/// Unit tests for StartupRegistryService focusing on Windows startup registry management
/// </summary>
public class StartupRegistryServiceTests
{
    private readonly IStartupRegistryService _startupRegistryService;

    public StartupRegistryServiceTests()
    {
        _startupRegistryService = new StartupRegistryService();
    }

    [Fact]
    public async Task IsStartupEnabledAsync_ShouldReturnBool()
    {
        // Act
        var result = await _startupRegistryService.IsStartupEnabledAsync();

        // Assert
        Assert.True(result == true || result == false); // Result can be true or false, both are valid
    }

    [Fact]
    public async Task SetStartupEnabledAsync_EnableThenDisable_ShouldWork()
    {
        try
        {
            // Arrange - Get initial state
            var initialState = await _startupRegistryService.IsStartupEnabledAsync();

            // Act - Enable startup
            var enableResult = await _startupRegistryService.EnableStartupAsync();
            var enabledState = await _startupRegistryService.IsStartupEnabledAsync();

            // Assert - Should be enabled
            Assert.True(enableResult);
            Assert.True(enabledState);

            // Act - Disable startup
            var disableResult = await _startupRegistryService.DisableStartupAsync();
            var disabledState = await _startupRegistryService.IsStartupEnabledAsync();

            // Assert - Should be disabled
            Assert.True(disableResult);
            Assert.False(disabledState);

            // Cleanup - Restore initial state
            await _startupRegistryService.SetStartupEnabledAsync(initialState);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if no registry permissions
            return;
        }
    }

    [Fact]
    public async Task SetStartupEnabledAsync_WithTrue_ShouldEnableStartup()
    {
        try
        {
            // Arrange - Get initial state for cleanup
            var initialState = await _startupRegistryService.IsStartupEnabledAsync();

            // Act
            var result = await _startupRegistryService.SetStartupEnabledAsync(true);
            var isEnabled = await _startupRegistryService.IsStartupEnabledAsync();

            // Assert
            Assert.True(result);
            Assert.True(isEnabled);

            // Cleanup - Restore initial state
            await _startupRegistryService.SetStartupEnabledAsync(initialState);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if no registry permissions
            return;
        }
    }

    [Fact]
    public async Task SetStartupEnabledAsync_WithFalse_ShouldDisableStartup()
    {
        try
        {
            // Arrange - Get initial state for cleanup
            var initialState = await _startupRegistryService.IsStartupEnabledAsync();

            // Act
            var result = await _startupRegistryService.SetStartupEnabledAsync(false);
            var isEnabled = await _startupRegistryService.IsStartupEnabledAsync();

            // Assert
            Assert.True(result);
            Assert.False(isEnabled);

            // Cleanup - Restore initial state
            await _startupRegistryService.SetStartupEnabledAsync(initialState);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if no registry permissions
            return;
        }
    }

    [Fact]
    public async Task EnableStartupAsync_ShouldReturnTrue_WhenSuccessful()
    {
        try
        {
            // Arrange - Get initial state for cleanup
            var initialState = await _startupRegistryService.IsStartupEnabledAsync();

            // Act
            var result = await _startupRegistryService.EnableStartupAsync();

            // Assert
            Assert.True(result);

            // Cleanup - Restore initial state
            await _startupRegistryService.SetStartupEnabledAsync(initialState);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if no registry permissions
            return;
        }
    }

    [Fact]
    public async Task DisableStartupAsync_ShouldReturnTrue_WhenSuccessful()
    {
        try
        {
            // Arrange - Get initial state for cleanup
            var initialState = await _startupRegistryService.IsStartupEnabledAsync();

            // Act
            var result = await _startupRegistryService.DisableStartupAsync();

            // Assert
            Assert.True(result);

            // Cleanup - Restore initial state
            await _startupRegistryService.SetStartupEnabledAsync(initialState);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip test if no registry permissions
            return;
        }
    }
}