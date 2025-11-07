using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using V_Launcher.ViewModels;

namespace V_Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon? _notifyIcon;
        private bool _isClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystemTray();
            
            // Handle window state changes
            StateChanged += MainWindow_StateChanged;
            
            // Subscribe to DataContext changes to wire up ViewModel events
            DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel events
            if (e.OldValue is MainViewModel oldViewModel)
            {
                oldViewModel.ShowWindowRequested -= OnShowWindowRequested;
                oldViewModel.MinimizeToTrayRequested -= OnMinimizeToTrayRequested;
            }

            // Subscribe to new ViewModel events
            if (e.NewValue is MainViewModel newViewModel)
            {
                newViewModel.ShowWindowRequested += OnShowWindowRequested;
                newViewModel.MinimizeToTrayRequested += OnMinimizeToTrayRequested;
            }
        }

        private void OnShowWindowRequested(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => RestoreFromTray());
        }

        private void OnMinimizeToTrayRequested(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => MinimizeToTray());
        }

        /// <summary>
        /// Gets the MainViewModel from the DataContext
        /// </summary>
        public MainViewModel? ViewModel => DataContext as MainViewModel;

        private void InitializeSystemTray()
        {
            // Create system tray icon
            _notifyIcon = new NotifyIcon();
            
            // Load the application icon from embedded resource
            try
            {
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/V-Launcher_Logo.png"))?.Stream;
                if (iconStream != null)
                {
                    using var bitmap = new Bitmap(iconStream);
                    _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
                else
                {
                    // Fallback to default system icon if logo not found
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                // Fallback to default system icon on any error
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Text = "AD User Launcher";
            _notifyIcon.Visible = false;

            // Handle double-click to restore window
            _notifyIcon.DoubleClick += (sender, e) => RestoreFromTray();

            // Create context menu for system tray
            var contextMenu = new ContextMenuStrip();
            
            var showItem = new ToolStripMenuItem("Show AD User Launcher");
            showItem.Click += (sender, e) => RestoreFromTray();
            contextMenu.Items.Add(showItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) => ExitApplication();
            contextMenu.Items.Add(exitItem);
            
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Check if we should minimize to tray
                if (ViewModel?.ApplicationSettings.MinimizeOnClose == true)
                {
                    MinimizeToTray();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Check if we should minimize to tray instead of closing
            if (!_isClosing && ViewModel?.ApplicationSettings.MinimizeOnClose == true)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from ViewModel events
            if (ViewModel != null)
            {
                ViewModel.ShowWindowRequested -= OnShowWindowRequested;
                ViewModel.MinimizeToTrayRequested -= OnMinimizeToTrayRequested;
            }

            // Clean up system tray icon
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            // Dispose the ViewModel when the window is closed
            ViewModel?.Dispose();
            base.OnClosed(e);
        }

        private void MinimizeToTray()
        {
            Hide();
            ShowInTaskbar = false;
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
                
                // Show balloon tip on first minimize
                if (!_notifyIcon.Visible)
                {
                    _notifyIcon.ShowBalloonTip(3000, "AD User Launcher", 
                        "Application minimized to system tray. Double-click the tray icon to restore.", 
                        ToolTipIcon.Info);
                }
            }
        }

        private void RestoreFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        private void ExitApplication()
        {
            _isClosing = true;
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Public method to show the window from external code (e.g., from App.xaml.cs)
        /// </summary>
        public void ShowWindow()
        {
            if (WindowState == WindowState.Minimized || !IsVisible)
            {
                RestoreFromTray();
            }
            else
            {
                Show();
                Activate();
            }
        }

        /// <summary>
        /// Public method to minimize to tray from external code
        /// </summary>
        public void MinimizeToTrayExternal()
        {
            MinimizeToTray();
        }
    }
}