namespace V_Launcher.Models;

/// <summary>
/// Represents a configured network drive mapping.
/// </summary>
public class NetworkDriveConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public string RemotePath { get; set; } = string.Empty;

    public Guid ADAccountId { get; set; }
}
