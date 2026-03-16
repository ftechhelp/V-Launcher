namespace V_Launcher.Models;

/// <summary>
/// Root configuration object containing all application data
/// </summary>
public class ApplicationConfiguration
{
    /// <summary>
    /// Collection of configured AD accounts
    /// </summary>
    public List<ADAccount> ADAccounts { get; set; } = new();

    /// <summary>
    /// Collection of executable configurations
    /// </summary>
    public List<ExecutableConfiguration> ExecutableConfigurations { get; set; } = new();

    /// <summary>
    /// Collection of network drive configurations
    /// </summary>
    public List<NetworkDriveConfiguration> NetworkDriveConfigurations { get; set; } = new();

    /// <summary>
    /// Application settings for startup behavior and window management
    /// </summary>
    public ApplicationSettings Settings { get; set; } = new();

    /// <summary>
    /// Whether OTP (two-factor) authentication is enabled for application launch
    /// </summary>
    public bool IsOtpEnabled { get; set; }

    /// <summary>
    /// DPAPI-encrypted TOTP secret key bytes, or null if OTP is not configured
    /// </summary>
    public byte[]? OtpEncryptedSecret { get; set; }

    /// <summary>
    /// Configuration file version for future compatibility
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Timestamp when the configuration was last saved
    /// </summary>
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
}