using System.IO;
using V_Launcher.Models;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

/// <summary>
/// Unit tests for ExecutableService focusing on icon extraction, caching, and path validation
/// </summary>
public class ExecutableServiceTests : IDisposable
{
    private readonly ExecutableService _executableService;
    private readonly ConfigurationRepository _configurationRepository;
    private readonly string _tempConfigPath;
    private readonly string _tempTestDir;

    public ExecutableServiceTests()
    {
        // Create temporary directory for test files
        _tempTestDir = Path.Combine(Path.GetTempPath(), "ExecutableServiceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempTestDir);

        // Create temporary configuration file
        _tempConfigPath = Path.Combine(_tempTestDir, "test_config.json");
        _configurationRepository = new ConfigurationRepository(_tempConfigPath);
        _executableService = new ExecutableService(_configurationRepository);
    }

    public void Dispose()
    {
        // Clean up temporary files
        if (Directory.Exists(_tempTestDir))
        {
            Directory.Delete(_tempTestDir, true);
        }
    }

    [Fact]
    public void ValidateExecutablePath_WithValidExePath_ReturnsTrue()
    {
        // Arrange - Use a known Windows executable
        var executablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

        // Act
        var isValid = _executableService.ValidateExecutablePath(executablePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithNonExistentPath_ReturnsFalse()
    {
        // Arrange
        var executablePath = Path.Combine(_tempTestDir, "nonexistent.exe");

        // Act
        var isValid = _executableService.ValidateExecutablePath(executablePath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithNullPath_ReturnsFalse()
    {
        // Act
        var isValid = _executableService.ValidateExecutablePath(null!);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        var isValid = _executableService.ValidateExecutablePath(string.Empty);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithNonExecutableFile_ReturnsFalse()
    {
        // Arrange - Create a text file
        var textFilePath = Path.Combine(_tempTestDir, "test.txt");
        File.WriteAllText(textFilePath, "This is not an executable");

        // Act
        var isValid = _executableService.ValidateExecutablePath(textFilePath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithMscFile_ReturnsTrue()
    {
        // Arrange - Create a dummy .msc file
        var mscFilePath = Path.Combine(_tempTestDir, "test.msc");
        File.WriteAllText(mscFilePath, "<?xml version=\"1.0\"?><MMC_ConsoleFile></MMC_ConsoleFile>");

        // Act
        var isValid = _executableService.ValidateExecutablePath(mscFilePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithVbsFile_ReturnsTrue()
    {
        // Arrange - Create a dummy .vbs file
        var vbsFilePath = Path.Combine(_tempTestDir, "test.vbs");
        File.WriteAllText(vbsFilePath, "WScript.Echo \"Test\"");

        // Act
        var isValid = _executableService.ValidateExecutablePath(vbsFilePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithPs1File_ReturnsTrue()
    {
        // Arrange - Create a dummy .ps1 file
        var ps1FilePath = Path.Combine(_tempTestDir, "test.ps1");
        File.WriteAllText(ps1FilePath, "Write-Host \"Test\"");

        // Act
        var isValid = _executableService.ValidateExecutablePath(ps1FilePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ValidateExecutablePath_WithWsfFile_ReturnsTrue()
    {
        // Arrange - Create a dummy .wsf file
        var wsfFilePath = Path.Combine(_tempTestDir, "test.wsf");
        File.WriteAllText(wsfFilePath, "<?xml version=\"1.0\"?><job></job>");

        // Act
        var isValid = _executableService.ValidateExecutablePath(wsfFilePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ExtractExecutableIconAsync_WithValidExe_ReturnsIcon()
    {
        // Arrange - Use notepad.exe which should have an icon
        var executablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

        // Act
        var icon = await _executableService.ExtractExecutableIconAsync(executablePath);

        // Assert
        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public async Task ExtractExecutableIconAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var executablePath = Path.Combine(_tempTestDir, "nonexistent.exe");

        // Act
        var icon = await _executableService.ExtractExecutableIconAsync(executablePath);

        // Assert
        Assert.Null(icon);
    }

    [Fact]
    public async Task ExtractExecutableIconAsync_WithNullPath_ReturnsNull()
    {
        // Act
        var icon = await _executableService.ExtractExecutableIconAsync(null!);

        // Assert
        Assert.Null(icon);
    }

    [Fact]
    public async Task LoadCustomIconAsync_WithValidImageFile_ReturnsIcon()
    {
        // Arrange - Create a simple bitmap file
        var iconPath = Path.Combine(_tempTestDir, "test_icon.png");
        CreateTestPngFile(iconPath);

        // Act
        var icon = await _executableService.LoadCustomIconAsync(iconPath);

        // Assert
        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public async Task LoadCustomIconAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var iconPath = Path.Combine(_tempTestDir, "nonexistent.png");

        // Act
        var icon = await _executableService.LoadCustomIconAsync(iconPath);

        // Assert
        Assert.Null(icon);
    }

    [Fact]
    public async Task LoadCustomIconAsync_WithNullPath_ReturnsNull()
    {
        // Act
        var icon = await _executableService.LoadCustomIconAsync(null!);

        // Assert
        Assert.Null(icon);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfiguration_SavesSuccessfully()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            DisplayName = "Test App",
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            ADAccountId = Guid.NewGuid()
        };

        // Act
        var savedConfig = await _executableService.SaveConfigurationAsync(config);

        // Assert
        Assert.Equal(config.Id, savedConfig.Id);
        Assert.Equal(config.DisplayName, savedConfig.DisplayName);
        Assert.Equal(config.ExecutablePath, savedConfig.ExecutablePath);

        // Verify it was actually saved
        var configurations = await _executableService.GetConfigurationsAsync();
        Assert.Single(configurations);
        Assert.Equal(config.Id, configurations.First().Id);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _executableService.SaveConfigurationAsync(null!));
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithEmptyDisplayName_ThrowsArgumentException()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            DisplayName = string.Empty,
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            ADAccountId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _executableService.SaveConfigurationAsync(config));
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithInvalidExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            DisplayName = "Test App",
            ExecutablePath = Path.Combine(_tempTestDir, "nonexistent.exe"),
            ADAccountId = Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _executableService.SaveConfigurationAsync(config));
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithMscFile_SavesSuccessfully()
    {
        // Arrange - Create a dummy .msc file
        var mscFilePath = Path.Combine(_tempTestDir, "test.msc");
        File.WriteAllText(mscFilePath, "<?xml version=\"1.0\"?><MMC_ConsoleFile></MMC_ConsoleFile>");

        var config = new ExecutableConfiguration
        {
            DisplayName = "Test MMC Console",
            ExecutablePath = mscFilePath,
            ADAccountId = Guid.NewGuid()
        };

        // Act
        var savedConfig = await _executableService.SaveConfigurationAsync(config);

        // Assert
        Assert.Equal(config.Id, savedConfig.Id);
        Assert.Equal(config.DisplayName, savedConfig.DisplayName);
        Assert.Equal(config.ExecutablePath, savedConfig.ExecutablePath);

        // Verify it was actually saved
        var configurations = await _executableService.GetConfigurationsAsync();
        Assert.Single(configurations);
        Assert.Equal(config.Id, configurations.First().Id);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_WithExistingConfiguration_RemovesConfiguration()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            DisplayName = "Test App",
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            ADAccountId = Guid.NewGuid()
        };
        await _executableService.SaveConfigurationAsync(config);

        // Act
        await _executableService.DeleteConfigurationAsync(config.Id);

        // Assert
        var configurations = await _executableService.GetConfigurationsAsync();
        Assert.Empty(configurations);
    }

    [Fact]
    public async Task GetIconAsync_WithCustomIconPath_ReturnsCustomIcon()
    {
        // Arrange
        var iconPath = Path.Combine(_tempTestDir, "custom_icon.png");
        CreateTestPngFile(iconPath);

        var config = new ExecutableConfiguration
        {
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            CustomIconPath = iconPath
        };

        // Act
        var icon = await _executableService.GetIconAsync(config);

        // Assert
        Assert.NotNull(icon);
    }

    [Fact]
    public async Task GetIconAsync_WithoutCustomIcon_ReturnsExecutableIcon()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe")
        };

        // Act
        var icon = await _executableService.GetIconAsync(config);

        // Assert
        Assert.NotNull(icon);
    }

    [Fact]
    public async Task GetIconAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _executableService.GetIconAsync(null!));
    }

    [Fact]
    public void ClearIconCache_ClearsAllCachedIcons()
    {
        // Arrange - This test verifies the method doesn't throw
        // The actual cache clearing is tested indirectly through other tests

        // Act & Assert - Should not throw
        _executableService.ClearIconCache();
    }

    [Fact]
    public async Task SaveConfigurationAsync_WhenUpdatingCustomIcon_ClearsBothOldAndNewIconFromCache()
    {
        // Arrange - Create two test icon files
        var oldIconPath = Path.Combine(_tempTestDir, "old_icon.png");
        var newIconPath = Path.Combine(_tempTestDir, "new_icon.png");
        CreateTestPngFile(oldIconPath);
        CreateTestPngFile(newIconPath);

        // Create initial configuration with old custom icon
        var config = new ExecutableConfiguration
        {
            DisplayName = "Test App",
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            CustomIconPath = oldIconPath,
            ADAccountId = Guid.NewGuid()
        };

        // Save initial configuration and load the old icon into cache
        var savedConfig = await _executableService.SaveConfigurationAsync(config);
        var oldIcon = await _executableService.GetIconAsync(savedConfig);
        Assert.NotNull(oldIcon);

        // Act - Update the configuration with a new custom icon
        savedConfig.CustomIconPath = newIconPath;
        await _executableService.SaveConfigurationAsync(savedConfig, oldIconPath);

        // Assert - Get the icon again, it should be the new one (not cached old one)
        var newIcon = await _executableService.GetIconAsync(savedConfig);
        Assert.NotNull(newIcon);
        
        // The icons should be different instances since cache was cleared
        Assert.NotSame(oldIcon, newIcon);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WhenClearingCustomIcon_ClearsOldIconFromCache()
    {
        // Arrange - Create a test icon file
        var customIconPath = Path.Combine(_tempTestDir, "custom_icon.png");
        CreateTestPngFile(customIconPath);

        // Create initial configuration with custom icon
        var config = new ExecutableConfiguration
        {
            DisplayName = "Test App",
            ExecutablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            CustomIconPath = customIconPath,
            ADAccountId = Guid.NewGuid()
        };

        // Save initial configuration and load the custom icon into cache
        var savedConfig = await _executableService.SaveConfigurationAsync(config);
        var customIcon = await _executableService.GetIconAsync(savedConfig);
        Assert.NotNull(customIcon);

        // Act - Clear the custom icon (set to null)
        savedConfig.CustomIconPath = null;
        await _executableService.SaveConfigurationAsync(savedConfig, customIconPath);

        // Assert - Get the icon again, it should now be the executable icon (not cached custom icon)
        var executableIcon = await _executableService.GetIconAsync(savedConfig);
        Assert.NotNull(executableIcon);
        
        // The icons should be different instances since we're now using executable icon
        Assert.NotSame(customIcon, executableIcon);
    }

    [Fact]
    public async Task IconCaching_SamePathRequestedTwice_UsesCachedResult()
    {
        // Arrange
        var executablePath = Path.Combine(Environment.SystemDirectory, "notepad.exe");

        // Act - Request the same icon twice
        var icon1 = await _executableService.ExtractExecutableIconAsync(executablePath);
        var icon2 = await _executableService.ExtractExecutableIconAsync(executablePath);

        // Assert - Both should be the same instance (cached)
        Assert.NotNull(icon1);
        Assert.NotNull(icon2);
        Assert.Same(icon1, icon2);
    }

    /// <summary>
    /// Creates a simple PNG file for testing custom icon loading
    /// </summary>
    private void CreateTestPngFile(string filePath)
    {
        // Create a simple 16x16 PNG file
        using var bitmap = new System.Drawing.Bitmap(16, 16);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        
        // Fill with a solid color
        graphics.Clear(System.Drawing.Color.Blue);
        
        // Save as PNG
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }
}