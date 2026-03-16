using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using V_Launcher.Services;

namespace V_Launcher.Views;

/// <summary>
/// Dialog window for setting up TOTP two-factor authentication.
/// Displays a QR code and manual secret key, then verifies a code before enabling OTP.
/// This window cannot be dismissed without completing setup or explicitly exiting the application.
/// </summary>
public partial class OtpSetupWindow : System.Windows.Window
{
    private readonly ITotpService _totpService;
    private readonly string _secretKey;

    /// <summary>
    /// Gets whether OTP setup was completed successfully.
    /// </summary>
    public bool SetupCompleted { get; private set; }

    public OtpSetupWindow(ITotpService totpService)
    {
        ArgumentNullException.ThrowIfNull(totpService);

        _totpService = totpService;
        _secretKey = totpService.GenerateSecretKey();

        InitializeComponent();

        DisplaySetupInformation();
    }

    private void DisplaySetupInformation()
    {
        // Show the secret key for manual entry
        SecretKeyTextBlock.Text = FormatSecretKey(_secretKey);

        // Generate and display QR code
        string otpAuthUri = _totpService.GenerateOtpAuthUri(_secretKey);
        QrCodeImage.Source = GenerateQrCodeImage(otpAuthUri);

        // Focus the verification code input
        VerificationCodeTextBox.Focus();
    }

    private static string FormatSecretKey(string key)
    {
        // Insert spaces every 4 characters for readability
        var formatted = new System.Text.StringBuilder();
        for (int i = 0; i < key.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
                formatted.Append(' ');
            formatted.Append(key[i]);
        }

        return formatted.ToString();
    }

    private static BitmapImage GenerateQrCodeImage(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        using QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);
        byte[] pngBytes = qrCode.GetGraphic(8);

        var bitmapImage = new BitmapImage();
        using (var stream = new MemoryStream(pngBytes))
        {
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
        }
        bitmapImage.Freeze();

        return bitmapImage;
    }

    private async void VerifyButton_Click(object? sender, System.Windows.RoutedEventArgs e)
    {
        string code = VerificationCodeTextBox.Text.Trim();

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ShowError("Please enter a valid 6-digit code.");
            return;
        }

        try
        {
            VerifyButton.IsEnabled = false;
            ExitAppButton.IsEnabled = false;
            ErrorMessageTextBlock.Visibility = System.Windows.Visibility.Collapsed;

            bool isValid = _totpService.ValidateCode(code, _secretKey);
            if (!isValid)
            {
                ShowError("Invalid code. Make sure your authenticator app is synced and try again.");
                VerifyButton.IsEnabled = true;
                ExitAppButton.IsEnabled = true;
                VerificationCodeTextBox.SelectAll();
                VerificationCodeTextBox.Focus();
                return;
            }

            // Code is valid — persist the secret and enable OTP
            await _totpService.EnableOtpAsync(_secretKey);
            SetupCompleted = true;

            System.Windows.MessageBox.Show(
                "Two-factor authentication has been enabled successfully.\n\nYou will need to enter a code from your authenticator app each time you launch V-Launcher.",
                "OTP Enabled",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to enable OTP: {ex.Message}");
            VerifyButton.IsEnabled = true;
            ExitAppButton.IsEnabled = true;
        }
    }

    private void ExitAppButton_Click(object? sender, System.Windows.RoutedEventArgs e)
    {
        SetupCompleted = false;
        DialogResult = false;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Allow closing only if setup was completed or user clicked Exit App
        if (!SetupCompleted && DialogResult != false)
        {
            e.Cancel = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessageTextBlock.Text = message;
        ErrorMessageTextBlock.Visibility = System.Windows.Visibility.Visible;
    }
}
