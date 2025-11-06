using System.Runtime.InteropServices;
using System.Security;

namespace V_Launcher.Helpers;

/// <summary>
/// Helper class for SecureString operations
/// </summary>
public static class SecureStringHelper
{
    /// <summary>
    /// Converts a SecureString to a regular string
    /// </summary>
    /// <param name="secureString">The SecureString to convert</param>
    /// <returns>The plain text string</returns>
    public static string ConvertToString(SecureString secureString)
    {
        if (secureString == null)
            return string.Empty;

        var ptr = Marshal.SecureStringToBSTR(secureString);
        try
        {
            return Marshal.PtrToStringBSTR(ptr) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeBSTR(ptr);
        }
    }

    /// <summary>
    /// Creates a SecureString from a regular string
    /// </summary>
    /// <param name="plainText">The plain text string</param>
    /// <returns>A new SecureString containing the text</returns>
    public static SecureString CreateFromString(string plainText)
    {
        var secureString = new SecureString();
        
        if (!string.IsNullOrEmpty(plainText))
        {
            foreach (char c in plainText)
            {
                secureString.AppendChar(c);
            }
        }
        
        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// Compares two SecureString instances for equality
    /// </summary>
    /// <param name="first">First SecureString</param>
    /// <param name="second">Second SecureString</param>
    /// <returns>True if the strings are equal, false otherwise</returns>
    public static bool AreEqual(SecureString? first, SecureString? second)
    {
        if (first == null && second == null)
            return true;
        
        if (first == null || second == null)
            return false;
        
        if (first.Length != second.Length)
            return false;

        var firstString = ConvertToString(first);
        var secondString = ConvertToString(second);
        
        try
        {
            return string.Equals(firstString, secondString, StringComparison.Ordinal);
        }
        finally
        {
            // Clear sensitive data from memory
            if (firstString.Length > 0)
            {
                unsafe
                {
                    fixed (char* ptr = firstString)
                    {
                        for (int i = 0; i < firstString.Length; i++)
                        {
                            ptr[i] = '\0';
                        }
                    }
                }
            }
            
            if (secondString.Length > 0)
            {
                unsafe
                {
                    fixed (char* ptr = secondString)
                    {
                        for (int i = 0; i < secondString.Length; i++)
                        {
                            ptr[i] = '\0';
                        }
                    }
                }
            }
        }
    }
}