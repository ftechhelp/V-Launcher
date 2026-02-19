using System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using V_Launcher.Models;
using V_Launcher.ViewModels;

namespace V_Launcher.Views
{
    /// <summary>
    /// Interaction logic for LauncherView.xaml
    /// </summary>
    public partial class LauncherView : System.Windows.Controls.UserControl
    {
        private System.Windows.Point? _dragStartPoint;
        private ExecutableItem? _draggedItem;

        public LauncherView()
        {
            InitializeComponent();
        }

        private void ExecutableItems_PreviewMouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
        {
            if (sender is not WpfControls.ListBox listBox || listBox.DataContext is not LauncherViewModel viewModel)
            {
                return;
            }

            if (!viewModel.IsCustomOrderMode)
            {
                _dragStartPoint = null;
                _draggedItem = null;
                return;
            }

            _dragStartPoint = e.GetPosition(listBox);
            _draggedItem = GetItemFromEvent(listBox, e.OriginalSource as DependencyObject);
        }

        private void ExecutableItems_MouseMove(object sender, WpfInput.MouseEventArgs e)
        {
            if (e.LeftButton != WpfInput.MouseButtonState.Pressed || _dragStartPoint is null || _draggedItem == null)
            {
                return;
            }

            if (sender is not WpfControls.ListBox listBox || listBox.DataContext is not LauncherViewModel viewModel || !viewModel.IsCustomOrderMode)
            {
                return;
            }

            var currentPosition = e.GetPosition(listBox);
            var horizontalDelta = Math.Abs(currentPosition.X - _dragStartPoint.Value.X);
            var verticalDelta = Math.Abs(currentPosition.Y - _dragStartPoint.Value.Y);

            if (horizontalDelta < SystemParameters.MinimumHorizontalDragDistance &&
                verticalDelta < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            System.Windows.DragDrop.DoDragDrop(listBox, _draggedItem, System.Windows.DragDropEffects.Move);
        }

        private void ExecutableItems_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is not WpfControls.ListBox listBox || listBox.DataContext is not LauncherViewModel viewModel || !viewModel.IsCustomOrderMode)
            {
                e.Effects = System.Windows.DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = e.Data.GetDataPresent(typeof(ExecutableItem))
                ? System.Windows.DragDropEffects.Move
                : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private async void ExecutableItems_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is not WpfControls.ListBox listBox || listBox.DataContext is not LauncherViewModel viewModel || !viewModel.IsCustomOrderMode)
            {
                return;
            }

            if (!e.Data.GetDataPresent(typeof(ExecutableItem)))
            {
                return;
            }

            var sourceItem = e.Data.GetData(typeof(ExecutableItem)) as ExecutableItem;
            if (sourceItem == null)
            {
                return;
            }

            var targetItem = GetItemFromEvent(listBox, e.OriginalSource as DependencyObject);

            try
            {
                await viewModel.MoveExecutableItemAsync(sourceItem, targetItem);
            }
            catch (InvalidOperationException)
            {
                // Error handling occurs within the view model.
            }
        }

        private async void OrderModeSelectionChanged(object sender, WpfControls.SelectionChangedEventArgs e)
        {
            if (sender is not WpfControls.ComboBox comboBox || comboBox.DataContext is not LauncherViewModel viewModel)
            {
                return;
            }

            if (comboBox.SelectedValue is not LauncherOrderMode orderMode)
            {
                return;
            }

            try
            {
                await viewModel.SetLauncherOrderModeAsync(orderMode);
            }
            catch (InvalidOperationException)
            {
                // Error handling occurs within the view model.
            }
        }

        private static ExecutableItem? GetItemFromEvent(WpfControls.ListBox listBox, DependencyObject? source)
        {
            if (source == null)
            {
                return null;
            }

            var container = WpfControls.ItemsControl.ContainerFromElement(listBox, source) as WpfControls.ListBoxItem;
            return container?.DataContext as ExecutableItem;
        }
    }
}