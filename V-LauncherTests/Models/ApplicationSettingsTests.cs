using V_Launcher.Models;
using Xunit;

namespace V_LauncherTests.Models;

/// <summary>
/// Unit tests for ApplicationSettings model
/// </summary>
public class ApplicationSettingsTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var settings = new ApplicationSettings();

        // Assert
        Assert.True(settings.StartOnWindowsStart);
        Assert.True(settings.StartMinimized);
        Assert.True(settings.MinimizeOnClose);
    }

    [Fact]
    public void Clone_ShouldCreateExactCopy()
    {
        // Arrange
        var original = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = true,
            MinimizeOnClose = false
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.StartOnWindowsStart, clone.StartOnWindowsStart);
        Assert.Equal(original.StartMinimized, clone.StartMinimized);
        Assert.Equal(original.MinimizeOnClose, clone.MinimizeOnClose);
    }

    [Fact]
    public void Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new ApplicationSettings();
        var clone = original.Clone();

        // Act
        original.StartOnWindowsStart = false;
        original.StartMinimized = false;
        original.MinimizeOnClose = false;

        // Assert
        Assert.True(clone.StartOnWindowsStart);
        Assert.True(clone.StartMinimized);
        Assert.True(clone.MinimizeOnClose);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void Equals_WithSameValues_ShouldReturnTrue(bool startOnWindowsStart, bool startMinimized, bool minimizeOnClose)
    {
        // Arrange
        var settings1 = new ApplicationSettings
        {
            StartOnWindowsStart = startOnWindowsStart,
            StartMinimized = startMinimized,
            MinimizeOnClose = minimizeOnClose
        };

        var settings2 = new ApplicationSettings
        {
            StartOnWindowsStart = startOnWindowsStart,
            StartMinimized = startMinimized,
            MinimizeOnClose = minimizeOnClose
        };

        // Act & Assert
        Assert.True(settings1.Equals(settings2));
        Assert.True(settings2.Equals(settings1));
    }

    [Fact]
    public void Equals_WithDifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var settings1 = new ApplicationSettings
        {
            StartOnWindowsStart = true,
            StartMinimized = true,
            MinimizeOnClose = true
        };

        var settings2 = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = true,
            MinimizeOnClose = true
        };

        // Act & Assert
        Assert.False(settings1.Equals(settings2));
        Assert.False(settings2.Equals(settings1));
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act & Assert
        Assert.False(settings.Equals(null));
    }

    [Fact]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        var settings = new ApplicationSettings();
        var otherObject = "not a settings object";

        // Act & Assert
        Assert.False(settings.Equals(otherObject));
    }

    [Fact]
    public void GetHashCode_WithSameValues_ShouldReturnSameHashCode()
    {
        // Arrange
        var settings1 = new ApplicationSettings
        {
            StartOnWindowsStart = true,
            StartMinimized = false,
            MinimizeOnClose = true
        };

        var settings2 = new ApplicationSettings
        {
            StartOnWindowsStart = true,
            StartMinimized = false,
            MinimizeOnClose = true
        };

        // Act
        var hash1 = settings1.GetHashCode();
        var hash2 = settings2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_WithDifferentValues_ShouldReturnDifferentHashCode()
    {
        // Arrange
        var settings1 = new ApplicationSettings
        {
            StartOnWindowsStart = true,
            StartMinimized = true,
            MinimizeOnClose = true
        };

        var settings2 = new ApplicationSettings
        {
            StartOnWindowsStart = false,
            StartMinimized = true,
            MinimizeOnClose = true
        };

        // Act
        var hash1 = settings1.GetHashCode();
        var hash2 = settings2.GetHashCode();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Properties_ShouldBeSettableAndGettable()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act & Assert
        settings.StartOnWindowsStart = false;
        Assert.False(settings.StartOnWindowsStart);

        settings.StartMinimized = false;
        Assert.False(settings.StartMinimized);

        settings.MinimizeOnClose = false;
        Assert.False(settings.MinimizeOnClose);
    }
}