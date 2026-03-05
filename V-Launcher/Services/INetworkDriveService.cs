using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service interface for managing and opening configured network drives.
/// </summary>
public interface INetworkDriveService
{
    Task<IEnumerable<NetworkDriveConfiguration>> GetConfigurationsAsync();

    Task<NetworkDriveConfiguration> SaveConfigurationAsync(NetworkDriveConfiguration configuration);

    Task DeleteConfigurationAsync(Guid configurationId);

    Task OpenDriveAsync(NetworkDriveConfiguration configuration, ADAccount account, string password);
}
