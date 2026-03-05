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

        private static string GetString(string name, string fallback)
        {
            return global::V_Launcher.Properties.Resources.ResourceManager.GetString(name) ?? fallback;
        }
    }
}
