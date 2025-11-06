using System.IO;
using V_Launcher.Models;
using V_Launcher.Services;
using Xunit;

namespace V_Launcher.Tests.Services;

/// <summary>
/// Unit tests for ProcessLauncher focusing on parameter validation and error handling
/// </summary>
public class ProcessLauncherTests
{
    private readonly ProcessLauncher _processLauncher;

    public ProcessLauncherTests()
    {
        _processLauncher = new ProcessLauncher();
    }

    #region ValidateConfiguration Tests

    [Fact]
    public void ValidateConfiguration_WithNullConfiguration_ReturnsFalse()
    {
        // Act
        var result = _processLauncher.ValidateConfiguration(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateConfiguration_WithEmptyExecutablePath_ReturnsFalse()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            ExecutablePath = string.Empty
        };

        // Act
        var result = _processLauncher.ValidateConfiguration(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateConfiguration_WithWhitespaceExecutablePath_ReturnsFalse()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            ExecutablePath = "   "
        };

        // Act
        var result = _processLauncher.ValidateConfiguration(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateConfiguration_WithNonExistentExecutablePath_ReturnsFalse()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            ExecutablePath = @"C:\NonExistent\Path\app.exe"
        };

        // Act
        var result = _processLauncher.ValidateConfiguration(config);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateConfiguration_WithValidExecutablePath_ReturnsTrue()
    {
        // Arrange - Create a temporary executable file for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe
            };

            // Act
            var result = _processLauncher.ValidateConfiguration(config);

            // Assert
            Assert.True(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidWorkingDirectory_ReturnsFalse()
    {
        // Arrange - Create a temporary executable file for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe,
                WorkingDirectory = @"C:\NonExistent\Directory"
            };

            // Act
            var result = _processLauncher.ValidateConfiguration(config);

            // Assert
            Assert.False(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithValidWorkingDirectory_ReturnsTrue()
    {
        // Arrange - Create a temporary executable file and directory for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);
        var tempDir = Path.GetTempPath();

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe,
                WorkingDirectory = tempDir
            };

            // Act
            var result = _processLauncher.ValidateConfiguration(config);

            // Assert
            Assert.True(result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    #endregion

    #region ValidateCredentials Tests

    [Fact]
    public void ValidateCredentials_WithNullAccount_ReturnsFalse()
    {
        // Act
        var result = _processLauncher.ValidateCredentials(null!, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithEmptyUsername_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = string.Empty,
            Domain = "testdomain"
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithWhitespaceUsername_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "   ",
            Domain = "testdomain"
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithEmptyDomain_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = string.Empty
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithWhitespaceDomain_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "   "
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, string.Empty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithWhitespacePassword_ReturnsFalse()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "   ");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCredentials_WithValidCredentials_ReturnsTrue()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act
        var result = _processLauncher.ValidateCredentials(account, "validpassword");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region LaunchAsync Tests

    [Fact]
    public async Task LaunchAsync_WithNullConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _processLauncher.LaunchAsync(null!, account, "password"));
        
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public async Task LaunchAsync_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var config = new ExecutableConfiguration
        {
            ExecutablePath = @"C:\NonExistent\app.exe"
        };
        var account = new ADAccount
        {
            Username = "testuser",
            Domain = "testdomain"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _processLauncher.LaunchAsync(config, account, "password"));
        
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public async Task LaunchAsync_WithNullAccount_ThrowsArgumentException()
    {
        // Arrange - Create a temporary executable file for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _processLauncher.LaunchAsync(config, null!, "password"));
            
            Assert.Equal("account", exception.ParamName);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    [Fact]
    public async Task LaunchAsync_WithInvalidCredentials_ThrowsArgumentException()
    {
        // Arrange - Create a temporary executable file for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe
            };
            var account = new ADAccount
            {
                Username = string.Empty, // Invalid username
                Domain = "testdomain"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                _processLauncher.LaunchAsync(config, account, "password"));
            
            Assert.Equal("account", exception.ParamName);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    [Fact]
    public async Task LaunchAsync_WithInvalidExecutableCredentials_ThrowsInvalidOperationException()
    {
        // Arrange - Create a temporary executable file for testing
        var tempFile = Path.GetTempFileName();
        var tempExe = Path.ChangeExtension(tempFile, ".exe");
        File.Move(tempFile, tempExe);

        try
        {
            var config = new ExecutableConfiguration
            {
                ExecutablePath = tempExe
            };
            var account = new ADAccount
            {
                Username = "invaliduser",
                Domain = "invaliddomain"
            };

            // Act & Assert - This should fail because the credentials are invalid
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _processLauncher.LaunchAsync(config, account, "invalidpassword"));
            
            Assert.Contains("Failed to launch process", exception.Message);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempExe))
                File.Delete(tempExe);
        }
    }

    #endregion
}