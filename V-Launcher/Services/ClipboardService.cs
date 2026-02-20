using System.Windows;

namespace V_Launcher.Services;

/// <summary>
/// Clipboard service backed by WPF clipboard APIs.
/// </summary>
public class ClipboardService : IClipboardService
{
    /// <summary>
    /// Copies the provided text to the clipboard.
    /// </summary>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        System.Windows.Clipboard.SetText(text);
    }
}
