namespace V_Launcher.Resources
{
    /// <summary>
    /// Provides localized strings for update workflow.
    /// </summary>
    public static class UpdateResources
    {
        public static string CheckUpdatesButtonLabel => GetString(nameof(CheckUpdatesButtonLabel), "Check Updates");
        public static string UpdateCheckInProgress => GetString(nameof(UpdateCheckInProgress), "Checking for updates...");
        public static string UpdateNoUpdateMessage => GetString(nameof(UpdateNoUpdateMessage), "You are running the latest version.");
        public static string UpdateCheckFailedMessage => GetString(nameof(UpdateCheckFailedMessage), "Unable to check for updates right now.");
        public static string UpdateAvailablePromptTitle => GetString(nameof(UpdateAvailablePromptTitle), "Update Available");
        public static string UpdateAvailablePromptBody => GetString(nameof(UpdateAvailablePromptBody), "A new version ({0}) is available. Install now?");
        public static string UpdateInstallStartedMessage => GetString(nameof(UpdateInstallStartedMessage), "Update installer started. The application will close.");
        public static string UpdateInstallFailedMessage => GetString(nameof(UpdateInstallFailedMessage), "Failed to start update installer.");
        public static string UpdateInstallDownloadFailedMessage => GetString(nameof(UpdateInstallDownloadFailedMessage), "Failed to download the update installer.");
        public static string UpdateInstallMissingChecksumMessage => GetString(nameof(UpdateInstallMissingChecksumMessage), "The update could not be verified because no checksum was published with the release.");
        public static string UpdateInstallChecksumMismatchMessage => GetString(nameof(UpdateInstallChecksumMismatchMessage), "The downloaded update failed checksum verification and was discarded.");
        public static string UpdateInstallUnsignedMessage => GetString(nameof(UpdateInstallUnsignedMessage), "Signature verification is enabled (VLAUNCHER_REQUIRE_INSTALLER_SIGNATURE=true) but the update installer is not digitally signed or its signature is not trusted.");
        public static string UpdateInstallLaunchFailedMessage => GetString(nameof(UpdateInstallLaunchFailedMessage), "The update was downloaded and verified but the installer could not be started.");

        private static string GetString(string name, string fallback)
        {
            return global::V_Launcher.Properties.Resources.ResourceManager.GetString(name) ?? fallback;
        }
    }
}
