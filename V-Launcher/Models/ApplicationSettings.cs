using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace V_Launcher.Models;

/// <summary>
/// Application settings for startup behavior and window management
/// </summary>
public class ApplicationSettings : INotifyPropertyChanged
{
    private bool _startOnWindowsStart = false;
    private bool _startMinimized = false;
    private bool _minimizeOnClose = false;
    private LauncherOrderMode _launcherOrderMode = LauncherOrderMode.Custom;

    /// <summary>
    /// Whether the application should start automatically when Windows starts
    /// </summary>
    public bool StartOnWindowsStart
    {
        get => _startOnWindowsStart;
        set
        {
            if (_startOnWindowsStart != value)
            {
                _startOnWindowsStart = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether the application should start minimized to the system tray
    /// </summary>
    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (_startMinimized != value)
            {
                _startMinimized = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether the application should minimize to tray instead of closing when the close button is clicked
    /// </summary>
    public bool MinimizeOnClose
    {
        get => _minimizeOnClose;
        set
        {
            if (_minimizeOnClose != value)
            {
                _minimizeOnClose = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Controls how launcher tiles are ordered.
    /// </summary>
    public LauncherOrderMode LauncherOrderMode
    {
        get => _launcherOrderMode;
        set
        {
            if (_launcherOrderMode != value)
            {
                _launcherOrderMode = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
            MinimizeOnClose = MinimizeOnClose,
            LauncherOrderMode = LauncherOrderMode
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
               MinimizeOnClose == settings.MinimizeOnClose &&
               LauncherOrderMode == settings.LauncherOrderMode;
    }

    /// <summary>
    /// Gets the hash code for the settings
    /// </summary>
    /// <returns>Hash code based on all property values</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(StartOnWindowsStart, StartMinimized, MinimizeOnClose, LauncherOrderMode);
    }
}