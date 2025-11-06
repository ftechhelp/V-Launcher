using System.Security;
using System.Security.Cryptography;
using System.Text;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service for managing AD account credentials with Windows DPAPI encryption
/// </summary>
public class CredentialService : ICredentialService
{
    private readonly List<ADAccount> _accounts = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Retrieves all stored AD accounts
    /// </summary>
    public Task<IEnumerable<ADAccount>> GetAccountsAsync()
    {
        lock (_lockObject)
        {
            return Task.FromResult(_accounts.AsEnumerable());
        }
    }

    /// <summary>
    /// Saves an AD account with encrypted password
    /// </summary>
    public Task<ADAccount> SaveAccountAsync(ADAccount account, string plainPassword)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));
        
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Password cannot be null or empty", nameof(plainPassword));

        // Encrypt the password
        account.EncryptedPassword = EncryptPassword(plainPassword);

        lock (_lockObject)
        {
            // Remove existing account with same ID if it exists
            var existingIndex = _accounts.FindIndex(a => a.Id == account.Id);
            if (existingIndex >= 0)
            {
                _accounts[existingIndex] = account;
            }
            else
            {
                _accounts.Add(account);
            }
        }

        return Task.FromResult(account);
    }

    /// <summary>
    /// Deletes an AD account by ID
    /// </summary>
    public Task DeleteAccountAsync(Guid accountId)
    {
        lock (_lockObject)
        {
            var accountToRemove = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (accountToRemove != null)
            {
                _accounts.Remove(accountToRemove);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Decrypts and returns the password for an AD account
    /// </summary>
    public Task<string> DecryptPasswordAsync(ADAccount account)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        if (account.EncryptedPassword == null || account.EncryptedPassword.Length == 0)
            throw new InvalidOperationException("Account has no encrypted password");

        var decryptedPassword = DecryptPassword(account.EncryptedPassword);
        return Task.FromResult(decryptedPassword);
    }

    /// <summary>
    /// Encrypts a plain text password using Windows DPAPI
    /// </summary>
    public byte[] EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Password cannot be null or empty", nameof(plainPassword));

        try
        {
            // Convert password to bytes
            var passwordBytes = Encoding.UTF8.GetBytes(plainPassword);
            
            // Encrypt using DPAPI with CurrentUser scope
            var encryptedBytes = ProtectedData.Protect(
                passwordBytes, 
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            // Clear the original password bytes from memory
            Array.Clear(passwordBytes, 0, passwordBytes.Length);

            return encryptedBytes;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to encrypt password using DPAPI", ex);
        }
    }

    /// <summary>
    /// Decrypts password bytes using Windows DPAPI
    /// </summary>
    public string DecryptPassword(byte[] encryptedPassword)
    {
        if (encryptedPassword == null || encryptedPassword.Length == 0)
            throw new ArgumentException("Encrypted password cannot be null or empty", nameof(encryptedPassword));

        try
        {
            // Decrypt using DPAPI
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedPassword,
                null, // No additional entropy
                DataProtectionScope.CurrentUser);

            // Convert bytes back to string
            var password = Encoding.UTF8.GetString(decryptedBytes);

            // Clear the decrypted bytes from memory
            Array.Clear(decryptedBytes, 0, decryptedBytes.Length);

            return password;
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to decrypt password using DPAPI", ex);
        }
    }
}