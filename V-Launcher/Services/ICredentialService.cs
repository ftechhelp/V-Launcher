using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service interface for managing AD account credentials with secure encryption
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Retrieves all stored AD accounts
    /// </summary>
    /// <returns>Collection of AD accounts with encrypted passwords</returns>
    Task<IEnumerable<ADAccount>> GetAccountsAsync();

    /// <summary>
    /// Saves an AD account with encrypted password
    /// </summary>
    /// <param name="account">The AD account to save</param>
    /// <param name="plainPassword">The plain text password to encrypt and store</param>
    /// <returns>The saved AD account with encrypted password</returns>
    Task<ADAccount> SaveAccountAsync(ADAccount account, string plainPassword);

    /// <summary>
    /// Deletes an AD account by ID
    /// </summary>
    /// <param name="accountId">The ID of the account to delete</param>
    Task DeleteAccountAsync(Guid accountId);

    /// <summary>
    /// Decrypts and returns the password for an AD account
    /// </summary>
    /// <param name="account">The AD account with encrypted password</param>
    /// <returns>The decrypted password</returns>
    Task<string> DecryptPasswordAsync(ADAccount account);

    /// <summary>
    /// Encrypts a plain text password using Windows DPAPI
    /// </summary>
    /// <param name="plainPassword">The plain text password to encrypt</param>
    /// <returns>Encrypted password bytes</returns>
    byte[] EncryptPassword(string plainPassword);

    /// <summary>
    /// Decrypts password bytes using Windows DPAPI
    /// </summary>
    /// <param name="encryptedPassword">The encrypted password bytes</param>
    /// <returns>The decrypted plain text password</returns>
    string DecryptPassword(byte[] encryptedPassword);
}