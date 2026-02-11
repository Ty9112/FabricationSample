using System.Windows;
using System.Windows.Threading;
using Autodesk.Fabrication;
using FabricationSample.Services.ItemSwap;
using FabricationSample.Windows;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class containing item swap functionality for DatabaseEditor.
    /// </summary>
    public partial class DatabaseEditor
    {
        private ItemSwapService _itemSwapService;
        private ItemSwapUndoManager _undoManager;

        /// <summary>
        /// Initializes the item swap service and undo manager.
        /// </summary>
        private void InitializeItemSwapServices()
        {
            _itemSwapService = new ItemSwapService();
            _undoManager = ItemSwapUndoManager.Instance;
            _undoManager.UndoStackChanged += UndoManager_UndoStackChanged;
            UpdateUndoButtonState();
        }

        /// <summary>
        /// Handles the undo stack changed event to update button state.
        /// Uses Dispatcher to ensure UI updates happen on the correct thread.
        /// </summary>
        private void UndoManager_UndoStackChanged(object sender, System.EventArgs e)
        {
            // Use Dispatcher to ensure UI updates happen on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                SafeInvoke(() => UpdateUndoButtonState());
            }
            else
            {
                UpdateUndoButtonState();
            }
        }

        /// <summary>
        /// Updates the undo button enabled state and tooltip.
        /// </summary>
        private void UpdateUndoButtonState()
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                SafeInvoke(() => UpdateUndoButtonState());
                return;
            }

            if (btnUndoSwap != null)
            {
                btnUndoSwap.IsEnabled = _undoManager?.CanUndo ?? false;
                if (_undoManager?.CanUndo == true)
                {
                    btnUndoSwap.ToolTip = $"Undo: {_undoManager.NextUndoDescription}";
                }
                else
                {
                    btnUndoSwap.ToolTip = "No swap operations to undo";
                }
            }
        }

        /// <summary>
        /// Handles the Swap Item button click.
        /// </summary>
        private void btnSwapItem_Click(object sender, RoutedEventArgs e)
        {
            // Ensure services are initialized
            if (_itemSwapService == null)
                InitializeItemSwapServices();

            // Get the selected item from the job items grid
            var selectedItem = dgJobItems.SelectedItem as Item;
            if (selectedItem == null)
            {
                MessageBox.Show("Please select an item from the Job Items list to swap.",
                    "No Item Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check if item has a service
            if (selectedItem.Service == null)
            {
                MessageBox.Show("The selected item does not have an associated service.",
                    "No Service", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Open the swap item window
            var swapWindow = new SwapItemWindow(selectedItem);
            swapWindow.Owner = Window.GetWindow(this);

            if (swapWindow.ShowDialog() == true && swapWindow.SwapExecuted)
            {
                // Refresh the job items grid
                RefreshJobItemsGrid();
                UpdateUndoButtonState();
            }
        }

        /// <summary>
        /// Handles the Undo Swap button click.
        /// </summary>
        private void btnUndoSwap_Click(object sender, RoutedEventArgs e)
        {
            // Ensure services are initialized
            if (_itemSwapService == null)
                InitializeItemSwapServices();

            if (!_undoManager.CanUndo)
            {
                MessageBox.Show("No swap operations to undo.",
                    "Nothing to Undo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirm the undo
            var undoDescription = _undoManager.NextUndoDescription;
            var result = MessageBox.Show(
                $"Are you sure you want to undo the following swap?\n\n{undoDescription}",
                "Confirm Undo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Execute the undo
            var undoResult = _itemSwapService.UndoLastSwap();

            if (undoResult.Success)
            {
                MessageBox.Show("Swap undone successfully.",
                    "Undo Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the job items grid
                RefreshJobItemsGrid();
                UpdateUndoButtonState();
            }
            else
            {
                MessageBox.Show($"Failed to undo swap: {undoResult.ErrorMessage}",
                    "Undo Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Refreshes the job items DataGrid.
        /// </summary>
        private void RefreshJobItemsGrid()
        {
            // Reload job items
            dgJobItems.ItemsSource = null;
            dgJobItems.ItemsSource = Job.Items;
        }
    }
}
