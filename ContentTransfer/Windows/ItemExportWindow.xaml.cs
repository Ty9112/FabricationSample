using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Autodesk.Fabrication.Content;

namespace FabricationSample.ContentTransfer.Windows
{
    public partial class ItemExportWindow : Window
    {
        /// <summary>
        /// The selected .itm file paths after the user clicks Export.
        /// </summary>
        public List<string> SelectedItemPaths { get; private set; }

        /// <summary>
        /// The output folder selected by the user.
        /// </summary>
        public string OutputFolder { get; private set; }

        private int _selectedCount;

        public ItemExportWindow()
        {
            InitializeComponent();
            SelectedItemPaths = new List<string>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var folders = new List<ItemFolder>(ItemFolders.Folders.OrderBy(x => x.Name));
                PopulateCheckboxTree(folders, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading item folders: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateSelectedCount();
        }

        #region Tree Population

        private void PopulateCheckboxTree(List<ItemFolder> folders, TreeViewItem parentItem)
        {
            foreach (var folder in folders)
            {
                var item = CreateFolderNode(folder);

                if (parentItem == null)
                    trvExportItems.Items.Add(item);
                else
                    parentItem.Items.Add(item);

                // Add dummy for lazy loading
                item.Items.Add("Loading...");
            }
        }

        private TreeViewItem CreateFolderNode(ItemFolder folder)
        {
            var checkBox = new CheckBox
            {
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += FolderCheckBox_Changed;
            checkBox.Unchecked += FolderCheckBox_Changed;

            var image = new Image
            {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri("pack://application:,,,/FabricationSample;component/Resources/Folder-32.png")),
                Margin = new Thickness(4, 0, 4, 0)
            };

            var label = new Label
            {
                Content = folder.Name,
                Padding = new Thickness(0)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(checkBox);
            stack.Children.Add(image);
            stack.Children.Add(label);

            var treeItem = new TreeViewItem
            {
                Header = stack,
                Tag = folder,
                FontWeight = FontWeights.Normal
            };

            return treeItem;
        }

        private TreeViewItem CreateFileNode(string filePath)
        {
            var checkBox = new CheckBox
            {
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = filePath
            };
            checkBox.Checked += FileCheckBox_Changed;
            checkBox.Unchecked += FileCheckBox_Changed;

            var image = new Image
            {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri("pack://application:,,,/FabricationSample;component/Resources/part.png")),
                Margin = new Thickness(4, 0, 4, 0)
            };

            var label = new Label
            {
                Content = Path.GetFileNameWithoutExtension(filePath),
                Padding = new Thickness(0)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(checkBox);
            stack.Children.Add(image);
            stack.Children.Add(label);

            var treeItem = new TreeViewItem
            {
                Header = stack,
                Tag = filePath,
                FontWeight = FontWeights.Normal
            };

            return treeItem;
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.Source as TreeViewItem;
            if (item == null || !item.HasItems)
                return;

            // Check for dummy "Loading..." item
            if (item.Items[0] is string)
            {
                item.Items.Clear();
                var folder = item.Tag as ItemFolder;
                if (folder != null)
                {
                    // Add sub-folders
                    PopulateCheckboxTree(folder.SubFolders.ToList(), item);

                    // Add .itm files
                    if (Directory.Exists(folder.Directory))
                    {
                        foreach (string f in Directory.GetFiles(folder.Directory, "*.itm", SearchOption.TopDirectoryOnly))
                        {
                            item.Items.Add(CreateFileNode(f));
                        }
                    }

                    // If the folder checkbox was checked, check all newly loaded children
                    var folderCheckBox = GetCheckBoxFromTreeItem(item);
                    if (folderCheckBox != null && folderCheckBox.IsChecked == true)
                    {
                        SetAllChildrenChecked(item, true);
                    }
                }
            }
        }

        #endregion

        #region Checkbox Handling

        private void FolderCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null)
                return;

            // Find the parent TreeViewItem
            var treeItem = FindParentTreeViewItem(checkBox);
            if (treeItem == null)
                return;

            bool isChecked = checkBox.IsChecked == true;
            SetAllChildrenChecked(treeItem, isChecked);
            UpdateSelectedCount();
        }

        private void FileCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void SetAllChildrenChecked(TreeViewItem parent, bool isChecked)
        {
            foreach (var child in parent.Items)
            {
                if (child is TreeViewItem childItem)
                {
                    var cb = GetCheckBoxFromTreeItem(childItem);
                    if (cb != null)
                        cb.IsChecked = isChecked;

                    // Recurse into sub-folders
                    SetAllChildrenChecked(childItem, isChecked);
                }
            }
        }

        private CheckBox GetCheckBoxFromTreeItem(TreeViewItem item)
        {
            if (item.Header is StackPanel stack)
            {
                foreach (var child in stack.Children)
                {
                    if (child is CheckBox cb)
                        return cb;
                }
            }
            return null;
        }

        private TreeViewItem FindParentTreeViewItem(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }

        #endregion

        #region Selection Counting

        private void UpdateSelectedCount()
        {
            _selectedCount = 0;
            CountSelectedItems(trvExportItems.Items);
            txtSelectedCount.Text = $"Selected: {_selectedCount} item{(_selectedCount == 1 ? "" : "s")}";
            UpdateExportButtonState();
        }

        private void CountSelectedItems(ItemCollection items)
        {
            foreach (var item in items)
            {
                if (item is TreeViewItem treeItem)
                {
                    // Check if this is a file node (Tag is string path)
                    if (treeItem.Tag is string)
                    {
                        var cb = GetCheckBoxFromTreeItem(treeItem);
                        if (cb != null && cb.IsChecked == true)
                            _selectedCount++;
                    }

                    CountSelectedItems(treeItem.Items);
                }
            }
        }

        private List<string> CollectSelectedPaths(ItemCollection items)
        {
            var paths = new List<string>();

            foreach (var item in items)
            {
                if (item is TreeViewItem treeItem)
                {
                    // File nodes have string Tag
                    if (treeItem.Tag is string path && path.EndsWith(".itm", StringComparison.OrdinalIgnoreCase))
                    {
                        var cb = GetCheckBoxFromTreeItem(treeItem);
                        if (cb != null && cb.IsChecked == true)
                            paths.Add(path);
                    }

                    paths.AddRange(CollectSelectedPaths(treeItem.Items));
                }
            }

            return paths;
        }

        #endregion

        #region Button Handlers

        private void btnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select output folder for export package";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtOutputFolder.Text = dialog.SelectedPath;
                    UpdateExportButtonState();
                }
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            SelectedItemPaths = CollectSelectedPaths(trvExportItems.Items);
            OutputFolder = txtOutputFolder.Text;

            if (SelectedItemPaths.Count == 0)
            {
                MessageBox.Show("No items selected.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(OutputFolder))
            {
                MessageBox.Show("Please select an output folder.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateExportButtonState()
        {
            btnExport.IsEnabled = _selectedCount > 0 && !string.IsNullOrEmpty(txtOutputFolder.Text);
        }

        #endregion
    }
}
