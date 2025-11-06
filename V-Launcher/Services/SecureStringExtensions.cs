using System.Runtime.InteropServices;
using System.Security;

namespace V_Launcher.Services;

/// <summary>
/// Extension methods for SecureString to support secure password handling
/// </summary>
public static class SecureStringExtensions
{
    /// <summary>
    /// Converts a SecureString to a regular string
    /// WARNING: This temporarily exposes the password in memory
    /// </summary>
    /// <param name="secureString">The SecureString to convert</param>
    /// <returns>The plain text string</returns>
    public static string ToUnsecureString(this SecureString secureString)
    {
        if (secureString == null)
            throw new ArgumentNullException(nameof(secureString));

        IntPtr unmanagedString = IntPtr.Zero;
        try
        {
            unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            return Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
        }
        finally
        {
            if (unmanagedString != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }

    /// <summary>
    /// Creates a SecureString from a regular string
    /// </summary>
    /// <param name="plainText">The plain text string</param>
    /// <returns>A SecureString containing the text</returns>
    public static SecureString ToSecureString(this string plainText)
    {
        if (plainText == null)
            throw new ArgumentNullException(nameof(plainText));

        var secureString = new SecureString();
        foreach (char c in plainText)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// Clears a string by overwriting its memory with zeros
    /// Note: This is not guaranteed to work due to string immutability in .NET
    /// Use SecureString for truly secure password handling
    /// </summary>
    /// <param name="text">The string to clear</param>
    public static void ClearString(ref string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            // Create a new string filled with zeros
            text = new string('\0', text.Length);
            text = string.Empty;
        }
    }
}