using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OtpNet;

namespace V_Launcher.Services;

/// <summary>
/// TOTP authentication service that generates and validates time-based one-time passwords.
/// Secret keys are encrypted using Windows DPAPI before being persisted to the configuration file.
/// </summary>
public class TotpService : ITotpService
{
    private const string Issuer = "V-Launcher";
    private const string AccountName = "User";
    private const int TotpStep = 30;
    private const int CodeDigits = 6;

    private readonly IConfigurationRepository _configurationRepository;
    private readonly ILogger<TotpService> _logger;

    private byte[]? _encryptedSecret;
    private bool _isLoaded;

    public TotpService(
        IConfigurationRepository configurationRepository,
        ILogger<TotpService> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsOtpConfigured => _isLoaded && _encryptedSecret is { Length: > 0 };

    /// <inheritdoc/>
    public string GenerateSecretKey()
    {
        var secret = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(secret);
    }

    /// <inheritdoc/>
    public string GenerateOtpAuthUri(string secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);
        return $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(AccountName)}" +
               $"?secret={secretKey}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits={CodeDigits}&period={TotpStep}";
    }

    /// <inheritdoc/>
    public bool ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (_encryptedSecret is not { Length: > 0 })
        {
            _logger.LogWarning("Attempted to validate OTP code but no secret is configured");
            return false;
        }

        try
        {
            var secretBytes = DecryptSecret(_encryptedSecret);
            var totp = new Totp(secretBytes, step: TotpStep, totpSize: CodeDigits);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt OTP secret during validation");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool ValidateCode(string code, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(secretKey))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(secretBytes, step: TotpStep, totpSize: CodeDigits);
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate OTP code against provided key");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task EnableOtpAsync(string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty.", nameof(secretKey));

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secretKey);
            _encryptedSecret = EncryptSecret(secretBytes);

            var config = await _configurationRepository.LoadConfigurationAsync();
            config.OtpEncryptedSecret = _encryptedSecret;
            config.IsOtpEnabled = true;
            await _configurationRepository.SaveConfigurationAsync(config);

            _logger.LogInformation("OTP authentication enabled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable OTP authentication");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ResetOtpAsync()
    {
        try
        {
            var config = await _configurationRepository.LoadConfigurationAsync();
            config.OtpEncryptedSecret = null;
            config.IsOtpEnabled = false;
            await _configurationRepository.SaveConfigurationAsync(config);

            _encryptedSecret = null;
            _isLoaded = true;

            _logger.LogInformation("OTP authentication reset successfully");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to reset OTP authentication");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task LoadConfigurationAsync()
    {
        try
        {
            var config = await _configurationRepository.LoadConfigurationAsync();
            _encryptedSecret = config.OtpEncryptedSecret;
            _isLoaded = true;

            _logger.LogInformation("OTP configuration loaded. HasSecret: {HasSecret}",
                _encryptedSecret is { Length: > 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load OTP configuration");
            _isLoaded = true;
            _encryptedSecret = null;
        }
    }

    private static byte[] EncryptSecret(byte[] secret)
    {
        return ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser);
    }

    private static byte[] DecryptSecret(byte[] encryptedSecret)
    {
        return ProtectedData.Unprotect(encryptedSecret, null, DataProtectionScope.CurrentUser);
    }
}
