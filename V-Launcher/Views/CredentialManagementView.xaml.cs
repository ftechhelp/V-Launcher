using System.Windows.Controls;
using V_Launcher.ViewModels;

namespace V_Launcher.Views
{
    /// <summary>
    /// Interaction logic for CredentialManagementView.xaml
    /// </summary>
    public partial class CredentialManagementView : System.Windows.Controls.UserControl
    {
        public CredentialManagementView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the CredentialManagementViewModel from the DataContext
        /// </summary>
        public CredentialManagementViewModel? ViewModel => DataContext as CredentialManagementViewModel;
    }
}