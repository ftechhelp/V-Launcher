namespace V_Launcher.Services;

/// <summary>
/// Service interface for TOTP (Time-based One-Time Password) authentication
/// compatible with Microsoft Authenticator and other TOTP apps.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Gets whether OTP authentication has been set up for this application instance.
    /// </summary>
    bool IsOtpConfigured { get; }

    /// <summary>
    /// Generates a new TOTP secret key for initial setup.
    /// </summary>
    /// <returns>Base32-encoded secret key</returns>
    string GenerateSecretKey();

    /// <summary>
    /// Generates the otpauth:// URI for use with authenticator apps such as Microsoft Authenticator.
    /// </summary>
    /// <param name="secretKey">Base32-encoded secret key</param>
    /// <returns>otpauth:// URI string</returns>
    string GenerateOtpAuthUri(string secretKey);

    /// <summary>
    /// Validates a TOTP code against the stored secret key.
    /// </summary>
    /// <param name="code">The 6-digit TOTP code entered by the user</param>
    /// <returns>True if the code is valid for the current or adjacent time window</returns>
    bool ValidateCode(string code);

    /// <summary>
    /// Validates a TOTP code against a specific secret key (used during initial setup before persisting).
    /// </summary>
    /// <param name="code">The 6-digit TOTP code entered by the user</param>
    /// <param name="secretKey">Base32-encoded secret key to validate against</param>
    /// <returns>True if the code is valid for the current or adjacent time window</returns>
    bool ValidateCode(string code, string secretKey);

    /// <summary>
    /// Completes OTP setup by encrypting and persisting the secret key.
    /// </summary>
    /// <param name="secretKey">Base32-encoded secret key to store</param>
    Task EnableOtpAsync(string secretKey);

    /// <summary>
    /// Clears the persisted OTP secret while preserving the rest of the application configuration.
    /// </summary>
    Task ResetOtpAsync();

    /// <summary>
    /// Loads the OTP configuration state from persistent storage.
    /// </summary>
    Task LoadConfigurationAsync();
}
