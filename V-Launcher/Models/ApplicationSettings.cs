namespace V_Launcher.Models;

/// <summary>
/// Application settings for startup behavior and window management
/// </summary>
public class ApplicationSettings
{
    /// <summary>
    /// Whether the application should start automatically when Windows starts
    /// </summary>
    public bool StartOnWindowsStart { get; set; } = false;

    /// <summary>
    /// Whether the application should start minimized to the system tray
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether the application should minimize to tray instead of closing when the close button is clicked
    /// </summary>
    public bool MinimizeOnClose { get; set; } = false;

    /// <summary>
    /// Creates a copy of the current settings
    /// </summary>
    /// <returns>A new ApplicationSettings instance with the same values</returns>
    public ApplicationSettings Clone()
    {
        return new ApplicationSettings
        {
            StartOnWindowsStart = StartOnWindowsStart,
            StartMinimized = StartMinimized,
            MinimizeOnClose = MinimizeOnClose
        };
    }

    /// <summary>
    /// Determines if the settings are equal to another settings object
    /// </summary>
    /// <param name="obj">The object to compare with</param>
    /// <returns>True if the settings are equal, false otherwise</returns>
    public override bool Equals(object? obj)
    {
        return obj is ApplicationSettings settings &&
               StartOnWindowsStart == settings.StartOnWindowsStart &&
               StartMinimized == settings.StartMinimized &&
               MinimizeOnClose == settings.MinimizeOnClose;
    }

    /// <summary>
    /// Gets the hash code for the settings
    /// </summary>
    /// <returns>Hash code based on all property values</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(StartOnWindowsStart, StartMinimized, MinimizeOnClose);
    }
}