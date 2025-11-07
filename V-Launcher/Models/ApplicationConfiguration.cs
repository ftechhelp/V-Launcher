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
    /// Application settings for startup behavior and window management
    /// </summary>
    public ApplicationSettings Settings { get; set; } = new();

    /// <summary>
    /// Configuration file version for future compatibility
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Timestamp when the configuration was last saved
    /// </summary>
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
}