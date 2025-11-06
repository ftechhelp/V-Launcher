using System.Security;
using V_Launcher.Services;
using Xunit;

namespace V_Launcher.Tests.Services;

/// <summary>
/// Unit tests for SecureString extension methods
/// </summary>
public class SecureStringExtensionsTests
{
    [Fact]
    public void ToSecureString_WithValidString_CreatesSecureString()
    {
        // Arrange
        const string plainText = "TestPassword123!";

        // Act
        using var secureString = plainText.ToSecureString();

        // Assert
        Assert.NotNull(secureString);
        Assert.Equal(plainText.Length, secureString.Length);
        Assert.True(secureString.IsReadOnly());
    }

    [Fact]
    public void ToSecureString_WithNullString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((string)null!).ToSecureString());
    }

    [Fact]
    public void ToUnsecureString_WithValidSecureString_ReturnsOriginalString()
    {
        // Arrange
        const string originalText = "TestPassword123!";
        using var secureString = originalText.ToSecureString();

        // Act
        var result = secureString.ToUnsecureString();

        // Assert
        Assert.Equal(originalText, result);
    }

    [Fact]
    public void ToUnsecureString_WithNullSecureString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((SecureString)null!).ToUnsecureString());
    }

    [Fact]
    public void SecureStringRoundtrip_PreservesOriginalValue()
    {
        // Arrange
        const string originalText = "ComplexP@ssw0rd!#$";

        // Act
        using var secureString = originalText.ToSecureString();
        var result = secureString.ToUnsecureString();

        // Assert
        Assert.Equal(originalText, result);
    }

    [Fact]
    public void ToSecureString_WithEmptyString_CreatesEmptySecureString()
    {
        // Arrange
        const string emptyText = "";

        // Act
        using var secureString = emptyText.ToSecureString();

        // Assert
        Assert.NotNull(secureString);
        Assert.Equal(0, secureString.Length);
    }

    [Fact]
    public void ClearString_WithValidString_ClearsReference()
    {
        // Arrange
        string testString = "TestPassword123!";
        string originalValue = testString;

        // Act
        SecureStringExtensions.ClearString(ref testString);

        // Assert
        Assert.NotEqual(originalValue, testString);
        Assert.True(string.IsNullOrEmpty(testString));
    }
}