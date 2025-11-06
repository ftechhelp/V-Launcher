using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace V_Launcher.Views
{
    /// <summary>
    /// A secure password box that binds to SecureString
    /// </summary>
    public partial class SecurePasswordBox : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty SecurePasswordProperty =
            DependencyProperty.Register(
                nameof(SecurePassword),
                typeof(SecureString),
                typeof(SecurePasswordBox),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSecurePasswordChanged));

        private bool _isUpdating;

        public SecurePasswordBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the SecureString password
        /// </summary>
        public SecureString? SecurePassword
        {
            get => (SecureString?)GetValue(SecurePasswordProperty);
            set => SetValue(SecurePasswordProperty, value);
        }

        private static void OnSecurePasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SecurePasswordBox passwordBox && !passwordBox._isUpdating)
            {
                passwordBox.UpdatePasswordBox();
            }
        }

        private void UpdatePasswordBox()
        {
            _isUpdating = true;
            try
            {
                if (SecurePassword == null)
                {
                    InternalPasswordBox.Clear();
                }
                else
                {
                    // We can't directly set the password from SecureString without converting to string
                    // This is a limitation of WPF PasswordBox, but we minimize exposure time
                    var plainPassword = ConvertSecureStringToString(SecurePassword);
                    InternalPasswordBox.Password = plainPassword;
                    
                    // Clear the plain text from memory immediately
                    if (plainPassword.Length > 0)
                    {
                        unsafe
                        {
                            fixed (char* ptr = plainPassword)
                            {
                                for (int i = 0; i < plainPassword.Length; i++)
                                {
                                    ptr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                // Dispose existing SecureString
                SecurePassword?.Dispose();

                // Create new SecureString from password
                var newSecureString = new SecureString();
                foreach (char c in InternalPasswordBox.Password)
                {
                    newSecureString.AppendChar(c);
                }
                newSecureString.MakeReadOnly();

                SecurePassword = newSecureString;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static string ConvertSecureStringToString(SecureString secureString)
        {
            if (secureString == null)
                return string.Empty;

            var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secureString);
            try
            {
                return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr) ?? string.Empty;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
        }
    }
}