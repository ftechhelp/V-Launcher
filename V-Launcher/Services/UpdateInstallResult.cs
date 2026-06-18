namespace V_Launcher.Services;

/// <summary>
/// Identifies why an update installation attempt did or did not start.
/// </summary>
public enum UpdateInstallFailureReason
{
    /// <summary>Installation started successfully.</summary>
    None,

    /// <summary>The update result had no available update or installer URL.</summary>
    NotInstallable,

    /// <summary>The installer could not be downloaded.</summary>
    DownloadFailed,

    /// <summary>No usable SHA-256 checksum was available to verify the download.</summary>
    MissingChecksum,

    /// <summary>The downloaded installer did not match the expected SHA-256 checksum.</summary>
    ChecksumMismatch,

    /// <summary>The downloaded installer failed Authenticode signature verification.</summary>
    SignatureVerificationFailed,

    /// <summary>The installer file downloaded and verified but the process failed to start.</summary>
    InstallerLaunchFailed,

    /// <summary>An unexpected error occurred while downloading or starting the installer.</summary>
    UnexpectedError
}

/// <summary>
/// Represents the outcome of an attempt to install an available update.
/// </summary>
public sealed record UpdateInstallResult(bool Started, UpdateInstallFailureReason Reason)
{
    /// <summary>A successful install-start outcome.</summary>
    public static UpdateInstallResult Success { get; } = new(true, UpdateInstallFailureReason.None);

    /// <summary>Creates a failed outcome with the supplied reason.</summary>
    public static UpdateInstallResult Failed(UpdateInstallFailureReason reason) => new(false, reason);
}
