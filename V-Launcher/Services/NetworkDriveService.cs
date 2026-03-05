using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service for validating, persisting, and opening configured network drives.
/// </summary>
public class NetworkDriveService : INetworkDriveService
{
    private const int NoError = 0;
    private const int ErrorSessionCredentialConflict = 1219;
    private const int ErrorAlreadyAssigned = 85;

    private readonly IConfigurationRepository _configurationRepository;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(ref NETRESOURCE lpNetResource, string? lpPassword, string? lpUserName, int dwFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string lpName, int dwFlags, bool fForce);

    public NetworkDriveService(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<IEnumerable<NetworkDriveConfiguration>> GetConfigurationsAsync()
    {
        return await _configurationRepository.LoadNetworkDriveConfigurationsAsync();
    }

    public async Task<NetworkDriveConfiguration> SaveConfigurationAsync(NetworkDriveConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(configuration));
        }

        if (!IsValidRemotePath(configuration.RemotePath))
        {
            throw new ArgumentException("Remote path must be a UNC path (for example: \\server\\share).", nameof(configuration));
        }

        if (configuration.ADAccountId == Guid.Empty)
        {
            throw new ArgumentException("An account must be selected.", nameof(configuration));
        }

        var configurations = (await GetConfigurationsAsync()).ToList();
        var existingIndex = configurations.FindIndex(item => item.Id == configuration.Id);

        if (existingIndex >= 0)
        {
            configurations[existingIndex] = configuration;
        }
        else
        {
            configurations.Add(configuration);
        }

        await _configurationRepository.SaveNetworkDriveConfigurationsAsync(configurations);
        return configuration;
    }

    public async Task DeleteConfigurationAsync(Guid configurationId)
    {
        var configurations = (await GetConfigurationsAsync()).ToList();
        var existing = configurations.FirstOrDefault(item => item.Id == configurationId);

        if (existing == null)
        {
            return;
        }

        configurations.Remove(existing);
        await _configurationRepository.SaveNetworkDriveConfigurationsAsync(configurations);
    }

    public async Task OpenDriveAsync(NetworkDriveConfiguration configuration, ADAccount account, string password)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(account);

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        if (!IsValidRemotePath(configuration.RemotePath))
        {
            throw new InvalidOperationException("Remote path must be a UNC path.");
        }

        await Task.Run(() => EnsureConnected(configuration.RemotePath, account.FullUsername, password));

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = configuration.RemotePath,
            UseShellExecute = true
        });
    }

    private static bool IsValidRemotePath(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            return false;
        }

        if (!remotePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = remotePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2;
    }

    private static void EnsureConnected(string remotePath, string username, string password)
    {
        var result = Connect(remotePath, username, password);

        if (result is NoError or ErrorAlreadyAssigned)
        {
            return;
        }

        if (result == ErrorSessionCredentialConflict)
        {
            DisconnectConnectionsForServer(remotePath);

            result = Connect(remotePath, username, password);
            if (result is NoError or ErrorAlreadyAssigned)
            {
                return;
            }

            throw new InvalidOperationException("Windows already has a connection to this server with different credentials. Existing server connections were disconnected but the new credential connection still failed.");
        }

        throw new InvalidOperationException($"Failed to connect to network drive. {new Win32Exception(result).Message} (Error code: {result})");
    }

    private static int Connect(string remotePath, string username, string password)
    {
        var resource = new NETRESOURCE
        {
            dwType = 1,
            lpRemoteName = remotePath
        };

        return WNetAddConnection2(ref resource, password, username, 0);
    }

    private static void DisconnectConnectionsForServer(string remotePath)
    {
        var serverName = GetServerName(remotePath);
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return;
        }

        var wildcardPath = $"\\\\{serverName}\\*";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "net.exe",
            Arguments = $"use {wildcardPath} /delete /y",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process != null)
        {
            process.WaitForExit();
        }

        // Best-effort direct disconnect for the remote path itself.
        _ = WNetCancelConnection2(remotePath, 0, true);
    }

    private static string? GetServerName(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath) || !remotePath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = remotePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }
}
