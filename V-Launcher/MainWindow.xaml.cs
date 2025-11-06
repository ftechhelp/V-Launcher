using System.Windows;
using V_Launcher.ViewModels;

namespace V_Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the MainViewModel from the DataContext
        /// </summary>
        public MainViewModel? ViewModel => DataContext as MainViewModel;

        protected override void OnClosed(EventArgs e)
        {
            // Dispose the ViewModel when the window is closed
            ViewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}