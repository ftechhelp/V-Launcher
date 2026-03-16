using System.Windows;
using System.Windows;
using System.Windows.Input;
using V_Launcher.Services;

namespace V_Launcher.Views;

/// <summary>
/// Dialog window for verifying a TOTP code at application launch.
/// Blocks application access until a valid code is provided.
/// </summary>
public partial class OtpVerificationWindow : System.Windows.Window
{
    private readonly ITotpService _totpService;
    private int _failedAttempts;
    private const int MaxAttempts = 5;

    /// <summary>
    /// Gets whether verification was successful.
    /// </summary>
    public bool IsVerified { get; private set; }

    public OtpVerificationWindow(ITotpService totpService)
    {
        ArgumentNullException.ThrowIfNull(totpService);

        _totpService = totpService;

        InitializeComponent();

        Loaded += (_, _) => CodeTextBox.Focus();
    }

    private void VerifyButton_Click(object? sender, System.Windows.RoutedEventArgs e)
    {
        AttemptVerification();
    }

    private void CodeTextBox_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptVerification();
            e.Handled = true;
        }
    }

    private void AttemptVerification()
    {
        string code = CodeTextBox.Text.Trim();

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            ShowError("Please enter a valid 6-digit code.");
            return;
        }

        bool isValid = _totpService.ValidateCode(code);
        if (isValid)
        {
            IsVerified = true;
            DialogResult = true;
            Close();
            return;
        }

        _failedAttempts++;

        if (_failedAttempts >= MaxAttempts)
        {
            System.Windows.MessageBox.Show(
                "Too many failed attempts. The application will now close.",
                "Authentication Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            DialogResult = false;
            Close();
            return;
        }

        int remaining = MaxAttempts - _failedAttempts;
        ShowError($"Invalid code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        CodeTextBox.SelectAll();
        CodeTextBox.Focus();
    }

    private void ExitButton_Click(object? sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorMessageTextBlock.Text = message;
        ErrorMessageTextBlock.Visibility = System.Windows.Visibility.Visible;
    }
}
