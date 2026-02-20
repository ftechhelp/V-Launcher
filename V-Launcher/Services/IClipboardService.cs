namespace V_Launcher.Services;

/// <summary>
/// Provides clipboard access for user-facing operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the provided text to the clipboard.
    /// </summary>
    void SetText(string text);
}
