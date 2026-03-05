namespace V_Launcher.Services;

/// <summary>
/// Represents the result of an update check operation.
/// </summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version? LatestVersion,
    string? LatestTag,
    string? InstallerUrl
);
