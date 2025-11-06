using System.Globalization;
using System.Windows.Data;

namespace V_Launcher.Views
{
    /// <summary>
    /// Converter that returns "Add Account" or "Edit Account" based on boolean value
    /// </summary>
    public class BoolToEditModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEditing)
            {
                return isEditing ? "Edit Account" : "Add Account";
            }
            return "Account Details";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}