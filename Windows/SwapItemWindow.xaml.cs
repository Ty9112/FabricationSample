using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using FabricationSample.Models;
using FabricationSample.Services.ItemSwap;

namespace FabricationSample.Windows
{
    /// <summary>
    /// Interaction logic for SwapItemWindow.xaml
    /// </summary>
    public partial class SwapItemWindow : Window
    {
        private Item _originalItem;
        private ItemSwapService _swapService;
        private ServiceButtonTreeItem _selectedButtonItem;

        /// <summary>
        /// Gets the result of the swap operation.
        /// </summary>
        public ItemSwapResult SwapResult { get; private set; }

        /// <summary>
        /// Gets whether the swap was executed.
        /// </summary>
        public bool SwapExecuted { get; private set; }

        /// <summary>
        /// Creates a new instance of SwapItemWindow.
        /// </summary>
        /// <param name="itemToSwap">The item to be swapped.</param>
        public SwapItemWindow(Item itemToSwap)
        {
            InitializeComponent();
            _originalItem = itemToSwap;
            _swapService = new ItemSwapService();
            LoadCurrentItemInfo();
            LoadServiceItems();
        }

        /// <summary>
        /// Loads the current item information into the UI.
        /// </summary>
        private void LoadCurrentItemInfo()
        {
            if (_originalItem == null)
                return;

            txtCurrentItemName.Text = _originalItem.Name ?? "Unknown";
            txtCurrentService.Text = _originalItem.Service?.Name ?? "Unknown";
            txtCurrentPattern.Text = _originalItem.PatternNumber.ToString();
            txtCurrentDescription.Text = _originalItem.SourceDescription ?? "";
        }

        /// <summary>
        /// Loads the service items into the TreeView.
        /// </summary>
        private void LoadServiceItems()
        {
            if (_originalItem?.Service?.ServiceTemplate?.ServiceTabs == null)
            {
                MessageBox.Show("Could not load service items.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rootItems = new ObservableCollection<ServiceTabTreeItem>();

            foreach (var tab in _originalItem.Service.ServiceTemplate.ServiceTabs)
            {
                var tabItem = new ServiceTabTreeItem
                {
                    Name = tab.Name ?? "Tab",
                    Tab = tab,
                    Items = new ObservableCollection<ServiceButtonTreeItem>()
                };

                foreach (var button in tab.ServiceButtons)
                {
                    var buttonItem = new ServiceButtonTreeItem
                    {
                        Name = button.Name ?? "Button",
                        Button = button,
                        Items = new ObservableCollection<ServiceButtonItemTreeItem>()
                    };

                    for (int i = 0; i < button.ServiceButtonItems.Count; i++)
                    {
                        var sbItem = button.ServiceButtonItems[i];
                        var itemTreeItem = new ServiceButtonItemTreeItem
                        {
                            Name = GetButtonItemDisplayName(sbItem, i),
                            ButtonItem = sbItem,
                            ButtonItemIndex = i,
                            ParentButton = button
                        };
                        buttonItem.Items.Add(itemTreeItem);
                    }

                    if (buttonItem.Items.Count > 0)
                        tabItem.Items.Add(buttonItem);
                }

                if (tabItem.Items.Count > 0)
                    rootItems.Add(tabItem);
            }

            tvServiceItems.ItemsSource = rootItems;
        }

        /// <summary>
        /// Gets a display name for a service button item.
        /// </summary>
        private string GetButtonItemDisplayName(ServiceButtonItem sbItem, int index)
        {
            string name = $"Item {index + 1}";

            try
            {
                // Try to get more info from the item path
                if (!string.IsNullOrEmpty(sbItem.ItemPath))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(sbItem.ItemPath);
                    if (!string.IsNullOrEmpty(fileName))
                        name = fileName;
                }

                // Add condition info if available
                if (sbItem.GreaterThan > 0 || sbItem.LessThanEqualTo > 0)
                {
                    name += $" ({sbItem.GreaterThan} - {sbItem.LessThanEqualTo})";
                }
            }
            catch { }

            return name;
        }

        /// <summary>
        /// Handles TreeView selection changed.
        /// </summary>
        private void tvServiceItems_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedButtonItem = null;
            btnSwap.IsEnabled = false;
            txtSelectedItem.Text = "";
            txtMatchingDimensions.Text = "";
            txtMatchingOptions.Text = "";

            if (e.NewValue is ServiceButtonItemTreeItem itemNode)
            {
                _selectedButtonItem = new ServiceButtonTreeItem
                {
                    Button = itemNode.ParentButton,
                    ButtonItemIndex = itemNode.ButtonItemIndex
                };

                txtSelectedItem.Text = itemNode.Name;
                btnSwap.IsEnabled = true;

                // Preview matching properties
                PreviewMatchingProperties(itemNode);
            }
            else if (e.NewValue is ServiceButtonTreeItem buttonNode)
            {
                // Selected a button, not an item - select first item if available
                if (buttonNode.Items.Count > 0)
                {
                    var firstItem = buttonNode.Items[0];
                    _selectedButtonItem = new ServiceButtonTreeItem
                    {
                        Button = buttonNode.Button,
                        ButtonItemIndex = 0
                    };
                    txtSelectedItem.Text = firstItem.Name;
                    btnSwap.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Previews which properties will match between original and new item.
        /// </summary>
        private void PreviewMatchingProperties(ServiceButtonItemTreeItem newItemNode)
        {
            try
            {
                // Load the new item temporarily to check matching properties
                var service = _originalItem.Service;
                var loadResult = service.LoadServiceItem(newItemNode.ParentButton, newItemNode.ButtonItem, false);

                if (loadResult.Status == Autodesk.Fabrication.Results.ResultStatus.Succeeded)
                {
                    var newItem = loadResult.ReturnObject as Item;
                    if (newItem != null)
                    {
                        // Find matching dimensions
                        var matchingDims = new List<string>();
                        foreach (var origDim in _originalItem.Dimensions)
                        {
                            if (newItem.Dimensions.Any(d => d.Name == origDim.Name))
                                matchingDims.Add(origDim.Name);
                        }
                        txtMatchingDimensions.Text = matchingDims.Count > 0
                            ? string.Join(", ", matchingDims)
                            : "None";

                        // Find matching options
                        var matchingOpts = new List<string>();
                        foreach (var origOpt in _originalItem.Options)
                        {
                            if (newItem.Options.Any(o => o.Name == origOpt.Name))
                                matchingOpts.Add(origOpt.Name);
                        }
                        txtMatchingOptions.Text = matchingOpts.Count > 0
                            ? string.Join(", ", matchingOpts)
                            : "None";

                        // Don't add this preview item to the job
                    }
                }
            }
            catch (Exception ex)
            {
                txtMatchingDimensions.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles the Swap button click.
        /// </summary>
        private void btnSwap_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedButtonItem == null)
            {
                MessageBox.Show("Please select an item to swap to.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm the swap
            var result = MessageBox.Show(
                $"Are you sure you want to swap '{_originalItem.Name}' with the selected item?\n\nThis operation can be undone.",
                "Confirm Swap",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Build swap options from checkboxes
            var options = new ItemSwapOptions
            {
                TransferPosition = chkTransferPosition.IsChecked ?? true,
                TransferDimensions = chkTransferDimensions.IsChecked ?? true,
                TransferOptions = chkTransferOptions.IsChecked ?? true,
                TransferCustomData = chkTransferCustomData.IsChecked ?? true,
                TransferBasicInfo = chkTransferBasicInfo.IsChecked ?? true,
                TransferStatusSection = chkTransferStatusSection.IsChecked ?? true,
                TransferPriceList = chkTransferPriceList.IsChecked ?? false
            };

            // Execute the swap
            SwapResult = _swapService.SwapItem(
                _originalItem,
                _selectedButtonItem.Button,
                _selectedButtonItem.ButtonItemIndex,
                options);

            if (SwapResult.Success)
            {
                SwapExecuted = true;

                string message = $"Item swapped successfully!\n\n{SwapResult.TransferResult?.Summary ?? ""}\n\nYou can undo this swap from the Job Items tab.";
                MessageBox.Show(message, "Swap Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show($"Swap failed: {SwapResult.ErrorMessage}", "Swap Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Cancel button click.
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles the Close button click.
        /// </summary>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles window dragging.
        /// </summary>
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }

    #region TreeView Item Classes

    /// <summary>
    /// Represents a service tab in the TreeView.
    /// </summary>
    public class ServiceTabTreeItem
    {
        public string Name { get; set; }
        public ServiceTab Tab { get; set; }
        public ObservableCollection<ServiceButtonTreeItem> Items { get; set; }
    }

    /// <summary>
    /// Represents a service button in the TreeView.
    /// </summary>
    public class ServiceButtonTreeItem
    {
        public string Name { get; set; }
        public ServiceButton Button { get; set; }
        public int ButtonItemIndex { get; set; }
        public ObservableCollection<ServiceButtonItemTreeItem> Items { get; set; }
    }

    /// <summary>
    /// Represents a service button item in the TreeView.
    /// </summary>
    public class ServiceButtonItemTreeItem
    {
        public string Name { get; set; }
        public ServiceButtonItem ButtonItem { get; set; }
        public int ButtonItemIndex { get; set; }
        public ServiceButton ParentButton { get; set; }
    }

    #endregion
}
