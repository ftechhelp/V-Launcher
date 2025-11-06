using System.ComponentModel.DataAnnotations;

namespace V_Launcher.Models;

/// <summary>
/// Represents an Active Directory user account with encrypted credentials
/// </summary>
public class ADAccount
{
    /// <summary>
    /// Unique identifier for the AD account
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the account shown in the UI
    /// </summary>
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Active Directory username
    /// </summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Active Directory domain
    /// </summary>
    [Required]
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted password bytes using Windows DPAPI
    /// </summary>
    public byte[] EncryptedPassword { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets the full username in domain\username format
    /// </summary>
    public string FullUsername => $"{Domain}\\{Username}";

    public override string ToString()
    {
        return DisplayName;
    }

    public override bool Equals(object? obj)
    {
        return obj is ADAccount account && Id.Equals(account.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
}