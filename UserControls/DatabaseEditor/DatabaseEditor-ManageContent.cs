using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.FunctionExamples;
using FabricationSample.Manager;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace FabricationSample.UserControls.DatabaseEditor
{
    public partial class DatabaseEditor : UserControl
    {
        #region Manage Content

        Item _currentContentItem;
        bool _contentCombosPopulated;
        bool _bulkCombosPopulated;
        ObservableCollection<FolderContentItem> _folderContents;
        ItemFolder _currentFolder;
        readonly Stack<ItemFolder> _folderNavHistory = new Stack<ItemFolder>();
        bool _navigatingBack;

        void PopulateContentCombos()
        {
            if (_contentCombosPopulated) return;

            var lstPrices = new List<string>() { "None" };
            lstPrices.AddRange(Database.SupplierGroups.SelectMany(x => x.PriceLists).Select(x => x.Name));
            cmbContentPriceList.ItemsSource = new ObservableCollection<string>(lstPrices);

            var itimes = new List<InstallationTimesTableBase>(Database.InstallationTimesTable.Cast<InstallationTimesTableBase>());
            itimes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            cmbContentInstallTimes.ItemsSource = itimes;
            cmbContentInstallTimes.DisplayMemberPath = "Name";

            var ftimes = new List<FabricationTimesTableBase>(Database.FabricationTimesTable.Cast<FabricationTimesTableBase>());
            ftimes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            cmbContentFabTimes.ItemsSource = ftimes;
            cmbContentFabTimes.DisplayMemberPath = "Name";

            _contentCombosPopulated = true;
        }

        void PopulateBulkCombos()
        {
            if (_bulkCombosPopulated) return;

            var lstPrices = new List<string>() { "" };
            lstPrices.AddRange(Database.SupplierGroups.SelectMany(x => x.PriceLists).Select(x => x.Name));
            cmbBulkContentPriceList.ItemsSource = new ObservableCollection<string>(lstPrices);

            var itimes = new List<InstallationTimesTableBase>(Database.InstallationTimesTable.Cast<InstallationTimesTableBase>());
            itimes.Insert(0, null);
            cmbBulkContentInstallTimes.ItemsSource = itimes;
            cmbBulkContentInstallTimes.DisplayMemberPath = "Name";

            var ftimes = new List<FabricationTimesTableBase>(Database.FabricationTimesTable.Cast<FabricationTimesTableBase>());
            ftimes.Insert(0, null);
            cmbBulkContentFabTimes.ItemsSource = ftimes;
            cmbBulkContentFabTimes.DisplayMemberPath = "Name";

            _bulkCombosPopulated = true;
        }

        void OnContentItemSelected(string path)
        {
            try
            {
                Item itm = ContentManager.LoadItem(path);
                if (itm == null)
                {
                    OnContentFolderSelected(null);
                    return;
                }

                _currentContentItem = itm;
                _currentFolder = null;
                PopulateContentCombos();

                // Image
                if (!string.IsNullOrEmpty(itm.ImagePath) && File.Exists(itm.ImagePath))
                    imgContentItem.Source = new ImageSourceConverter().ConvertFromString(itm.ImagePath) as ImageSource;
                else
                    imgContentItem.Source = null;

                // Identity
                txtContentCID.Text = itm.CID.ToString();
                txtContentDatabaseId.Text = itm.DatabaseId.ToString();
                txtContentService.Text = itm.Service != null ? itm.Service.Name : "";

                // Read-only
                txtContentMaterial.Text = itm.Material != null ? itm.Material.Name : "";
                txtContentSpecification.Text = itm.Specification != null ? itm.Specification.Name : "";
                txtContentSection.Text = itm.Section != null ? itm.Section.Description : "";

                // Price List
                if (itm.PriceList == null)
                    cmbContentPriceList.SelectedValue = "None";
                else
                    cmbContentPriceList.SelectedValue = itm.PriceList.Name;

                // Installation Times Table
                if (itm.InstallationTimesTable != null)
                {
                    var match = cmbContentInstallTimes.ItemsSource?.Cast<InstallationTimesTableBase>()
                        .FirstOrDefault(x => x.Name == itm.InstallationTimesTable.Name);
                    cmbContentInstallTimes.SelectedItem = match;
                }
                else
                    cmbContentInstallTimes.SelectedItem = null;

                // Fabrication Times Table
                if (itm.FabricationTimesTable != null)
                {
                    var match = cmbContentFabTimes.ItemsSource?.Cast<FabricationTimesTableBase>()
                        .FirstOrDefault(x => x.Name == itm.FabricationTimesTable.Name);
                    cmbContentFabTimes.SelectedItem = match;
                }
                else
                    cmbContentFabTimes.SelectedItem = null;

                // Editable text fields
                txtContentNotes.Text = itm.Notes ?? "";
                txtContentAlias.Text = itm.Alias ?? "";
                txtContentDrawingName.Text = itm.DrawingName ?? "";
                txtContentOrder.Text = itm.Order ?? "";
                txtContentZone.Text = itm.Zone ?? "";
                txtContentEquipmentTag.Text = itm.EquipmentTag ?? "";
                txtContentPallet.Text = itm.Pallet ?? "";
                txtContentSpoolName.Text = itm.SpoolName ?? "";

                pnlContentDetail.Visibility = Visibility.Visible;
                pnlFolderContents.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading item: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                OnContentFolderSelected(null);
            }
        }

        void OnContentFolderSelected(ItemFolder folder)
        {
            _currentContentItem = null;
            pnlContentDetail.Visibility = Visibility.Collapsed;

            // Reset toggle when navigating normally
            if (tglShowSubfolders != null)
                tglShowSubfolders.IsChecked = false;

            if (folder == null)
            {
                pnlFolderContents.Visibility = Visibility.Collapsed;
                _folderNavHistory.Clear();
                if (btnBackFolder != null) btnBackFolder.IsEnabled = false;
                return;
            }

            // Push current folder to history unless we're navigating back
            if (!_navigatingBack && _currentFolder != null)
                _folderNavHistory.Push(_currentFolder);

            if (btnBackFolder != null)
                btnBackFolder.IsEnabled = _folderNavHistory.Count > 0;

            _currentFolder = folder;
            PopulateBulkCombos();

            _folderContents = new ObservableCollection<FolderContentItem>();

            // Add subfolders
            if (folder.SubFolders != null)
            {
                foreach (var sub in folder.SubFolders.OrderBy(f => f.Name))
                {
                    _folderContents.Add(new FolderContentItem
                    {
                        IsFolder = true,
                        Name = sub.Name,
                        FullPath = sub.Directory,
                        Folder = sub,
                        TypeIcon = "\U0001F4C1"
                    });
                }
            }

            // Add .itm files
            if (Directory.Exists(folder.Directory))
            {
                foreach (string f in Directory.GetFiles(folder.Directory, "*.itm", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                {
                    var item = new FolderContentItem
                    {
                        IsFolder = false,
                        Name = Path.GetFileNameWithoutExtension(f),
                        FullPath = f,
                        TypeIcon = "\U0001F527"
                    };

                    // Try to load item metadata
                    try
                    {
                        Item itm = ContentManager.LoadItem(f);
                        if (itm != null)
                        {
                            item.CID = itm.CID.ToString();
                            item.ServiceName = itm.Service?.Name ?? "";
                            item.MaterialName = itm.Material?.Name ?? "";
                            item.SpecificationName = itm.Specification?.Name ?? "";
                            item.PriceListName = itm.PriceList?.Name ?? "";
                            item.InstallTimesName = itm.InstallationTimesTable?.Name ?? "";
                            item.FabTimesName = itm.FabricationTimesTable?.Name ?? "";
                            item.IsBoughtOut = itm.BoughtOut;
                        }
                    }
                    catch { }

                    _folderContents.Add(item);
                }
            }

            txtFolderPath.Text = folder.Name;
            dgFolderContents.ItemsSource = _folderContents;
            ApplyBlankFilter();
            pnlFolderContents.Visibility = Visibility.Visible;
        }

        private void dgFolderContents_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = dgFolderContents.SelectedItem as FolderContentItem;
            if (item == null) return;

            if (item.IsFolder && item.Folder != null)
            {
                // Navigate into subfolder - select it in the tree
                OnContentFolderSelected(item.Folder);
            }
            else if (!item.IsFolder && !string.IsNullOrEmpty(item.FullPath))
            {
                // Open item detail
                FabricationManager.CurrentLoadedItemPath = item.FullPath;
                OnContentItemSelected(item.FullPath);
            }
        }

        private void btnBackFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_folderNavHistory.Count == 0) return;
            _navigatingBack = true;
            var parent = _folderNavHistory.Pop();
            OnContentFolderSelected(parent);
            _navigatingBack = false;
        }

        private void cmbFilterBlankColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyBlankFilter();
        }

        void ApplyBlankFilter()
        {
            if (_folderContents == null) return;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_folderContents);
            if (view == null) return;

            var selected = (cmbFilterBlankColumn.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;

            if (string.IsNullOrEmpty(selected) || selected == "Off")
            {
                view.Filter = null;
                return;
            }

            view.Filter = obj =>
            {
                var item = obj as FolderContentItem;
                if (item == null || item.IsFolder) return true;

                if (selected == "AnyBlank")
                    return string.IsNullOrEmpty(item.SpecificationName)
                        || string.IsNullOrEmpty(item.PriceListName)
                        || string.IsNullOrEmpty(item.InstallTimesName)
                        || string.IsNullOrEmpty(item.FabTimesName);

                switch (selected)
                {
                    case "SpecificationName": return string.IsNullOrEmpty(item.SpecificationName);
                    case "PriceListName":     return string.IsNullOrEmpty(item.PriceListName);
                    case "InstallTimesName":  return string.IsNullOrEmpty(item.InstallTimesName);
                    case "FabTimesName":      return string.IsNullOrEmpty(item.FabTimesName);
                    default: return true;
                }
            };
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_folderContents == null) return;
            foreach (var item in _folderContents)
                if (!item.IsFolder) item.IsSelected = true;
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (_folderContents == null) return;
            foreach (var item in _folderContents)
                item.IsSelected = false;
        }

        private void btnApplyBulkContent_Click(object sender, RoutedEventArgs e)
        {
            if (_folderContents == null) return;

            var selectedItems = _folderContents.Where(x => x.IsSelected && !x.IsFolder).ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No items selected.", "Bulk Assignment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Cache bulk values
            string bulkPriceListName = cmbBulkContentPriceList.SelectedItem as string;
            var bulkInstallTable = cmbBulkContentInstallTimes.SelectedItem as InstallationTimesTableBase;
            var bulkFabTable = cmbBulkContentFabTimes.SelectedItem as FabricationTimesTableBase;
            string bulkNotes = txtBulkContentNotes.Text;
            string bulkAlias = txtBulkContentAlias.Text;
            string bulkDrawingName = txtBulkContentDrawingName.Text;
            string bulkOrder = txtBulkContentOrder.Text;
            string bulkZone = txtBulkContentZone.Text;
            string bulkEquipmentTag = txtBulkContentEquipmentTag.Text;
            string bulkPallet = txtBulkContentPallet.Text;
            string bulkSpoolName = txtBulkContentSpoolName.Text;
            // 0 = No Change, 1 = Yes (true), 2 = No (false)
            int bulkBoughtOutIdx = cmbBulkContentBoughtOut.SelectedIndex;

            // Resolve price list object
            PriceListBase bulkPriceList = null;
            if (!string.IsNullOrEmpty(bulkPriceListName))
                bulkPriceList = Database.SupplierGroups.SelectMany(x => x.PriceLists)
                    .FirstOrDefault(x => x.Name == bulkPriceListName);

            int successCount = 0;
            int failCount = 0;

            foreach (var sel in selectedItems)
            {
                try
                {
                    Item itm = ContentManager.LoadItem(sel.FullPath);
                    if (itm == null) { failCount++; continue; }

                    if (bulkPriceList != null)
                        itm.PriceList = bulkPriceList;
                    if (bulkInstallTable != null)
                        itm.InstallationTimesTable = bulkInstallTable;
                    if (bulkFabTable != null)
                        itm.FabricationTimesTable = bulkFabTable;
                    if (!string.IsNullOrEmpty(bulkNotes))
                        itm.Notes = bulkNotes;
                    if (!string.IsNullOrEmpty(bulkAlias))
                        itm.Alias = bulkAlias;
                    if (!string.IsNullOrEmpty(bulkDrawingName))
                        itm.DrawingName = bulkDrawingName;
                    if (!string.IsNullOrEmpty(bulkOrder))
                        itm.Order = bulkOrder;
                    if (!string.IsNullOrEmpty(bulkZone))
                        itm.Zone = bulkZone;
                    if (!string.IsNullOrEmpty(bulkEquipmentTag))
                        itm.EquipmentTag = bulkEquipmentTag;
                    if (!string.IsNullOrEmpty(bulkPallet))
                        itm.Pallet = bulkPallet;
                    if (!string.IsNullOrEmpty(bulkSpoolName))
                        itm.SpoolName = bulkSpoolName;
                    if (bulkBoughtOutIdx == 1)
                        itm.BoughtOut = true;
                    else if (bulkBoughtOutIdx == 2)
                        itm.BoughtOut = false;

                    ItemOperationResult result = ContentManager.SaveItem(itm);
                    if (result.Status == ResultStatus.Succeeded)
                        successCount++;
                    else
                        failCount++;
                }
                catch
                {
                    failCount++;
                }
            }

            // Clear bulk fields
            cmbBulkContentPriceList.SelectedIndex = -1;
            cmbBulkContentInstallTimes.SelectedIndex = -1;
            cmbBulkContentFabTimes.SelectedIndex = -1;
            txtBulkContentNotes.Text = "";
            txtBulkContentAlias.Text = "";
            txtBulkContentDrawingName.Text = "";
            txtBulkContentOrder.Text = "";
            txtBulkContentZone.Text = "";
            txtBulkContentEquipmentTag.Text = "";
            txtBulkContentPallet.Text = "";
            txtBulkContentSpoolName.Text = "";
            cmbBulkContentBoughtOut.SelectedIndex = 0;

            string msg = $"{successCount} item(s) updated successfully.";
            if (failCount > 0)
                msg += $"\n{failCount} item(s) failed.";

            MessageBox.Show(msg, "Bulk Assignment", MessageBoxButton.OK, MessageBoxImage.Information);

            // Refresh folder view
            if (_currentFolder != null)
                OnContentFolderSelected(_currentFolder);
        }

        private void btnSaveContentItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentContentItem == null) return;

            try
            {
                // Apply Price List
                string selectedPrice = cmbContentPriceList.SelectedValue as string;
                if (selectedPrice == "None")
                    _currentContentItem.PriceList = null;
                else if (!string.IsNullOrEmpty(selectedPrice))
                {
                    var pl = Database.SupplierGroups.SelectMany(x => x.PriceLists)
                        .FirstOrDefault(x => x.Name == selectedPrice);
                    if (pl != null)
                        _currentContentItem.PriceList = pl;
                }

                // Apply Installation Times
                var installTable = cmbContentInstallTimes.SelectedItem as InstallationTimesTableBase;
                if (installTable != null)
                    _currentContentItem.InstallationTimesTable = installTable;

                // Apply Fabrication Times
                var fabTable = cmbContentFabTimes.SelectedItem as FabricationTimesTableBase;
                if (fabTable != null)
                    _currentContentItem.FabricationTimesTable = fabTable;

                // Apply text fields
                if (!string.IsNullOrEmpty(txtContentNotes.Text) && txtContentNotes.Text != _currentContentItem.Notes)
                    _currentContentItem.Notes = txtContentNotes.Text;
                if (!string.IsNullOrEmpty(txtContentAlias.Text) && txtContentAlias.Text != _currentContentItem.Alias)
                    _currentContentItem.Alias = txtContentAlias.Text;
                if (!string.IsNullOrEmpty(txtContentDrawingName.Text) && txtContentDrawingName.Text != _currentContentItem.DrawingName)
                    _currentContentItem.DrawingName = txtContentDrawingName.Text;
                if (!string.IsNullOrEmpty(txtContentOrder.Text) && txtContentOrder.Text != _currentContentItem.Order)
                    _currentContentItem.Order = txtContentOrder.Text;
                if (!string.IsNullOrEmpty(txtContentZone.Text) && txtContentZone.Text != _currentContentItem.Zone)
                    _currentContentItem.Zone = txtContentZone.Text;
                if (!string.IsNullOrEmpty(txtContentEquipmentTag.Text) && txtContentEquipmentTag.Text != _currentContentItem.EquipmentTag)
                    _currentContentItem.EquipmentTag = txtContentEquipmentTag.Text;
                if (!string.IsNullOrEmpty(txtContentPallet.Text) && txtContentPallet.Text != _currentContentItem.Pallet)
                    _currentContentItem.Pallet = txtContentPallet.Text;
                if (!string.IsNullOrEmpty(txtContentSpoolName.Text) && txtContentSpoolName.Text != _currentContentItem.SpoolName)
                    _currentContentItem.SpoolName = txtContentSpoolName.Text;

                ItemOperationResult result = ContentManager.SaveItem(_currentContentItem);
                if (result.Status == ResultStatus.Succeeded)
                    MessageBox.Show("Item saved successfully.", "Save Content", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Failed to save item: " + result.Status, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving item: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddContentToJob_Click(object sender, RoutedEventArgs e)
        {
            if (_currentContentItem == null) return;

            FabricationManager.CurrentItem = _currentContentItem;
            FabricationAPIExamples.AddItemToJob(_currentContentItem);
        }

        private void btnCloneItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentContentItem == null) return;

            try
            {
                string sourcePath = FabricationManager.CurrentLoadedItemPath;
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    MessageBox.Show("No source item path available. Please open an item first.",
                        "Clone Item", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string sourceDir = Path.GetDirectoryName(sourcePath);
                string sourceNameNoExt = Path.GetFileNameWithoutExtension(sourcePath);
                string sourceTxtPath = Path.Combine(sourceDir, sourceNameNoExt + ".Txt");
                string sourcePngPath = Path.Combine(sourceDir, sourceNameNoExt + ".png");

                // Read source product list
                List<string[]> sourceEntries = new List<string[]>();
                string sourceDbIdBase = "";
                if (File.Exists(sourceTxtPath))
                {
                    var lines = File.ReadAllLines(sourceTxtPath);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        var parts = lines[i].Split(',');
                        if (parts.Length >= 4)
                            sourceEntries.Add(parts);
                    }

                    if (sourceEntries.Count > 0)
                    {
                        string firstId = sourceEntries[0][3].Trim();
                        int lastDash = firstId.LastIndexOf('-');
                        if (lastDash > 0)
                            sourceDbIdBase = firstId.Substring(0, lastDash);
                    }
                }

                // Prompt for new item name
                string defaultName = sourceNameNoExt.Replace("Standard", "Schedule 10S");
                string newItemName = ShowInputDialog(
                    "Clone Item — New Name",
                    $"Source: {sourceNameNoExt}\n" +
                    $"Product list: {sourceEntries.Count} entries\n" +
                    $"DatabaseId base: {sourceDbIdBase}\n\n" +
                    "New item name:",
                    defaultName);

                if (string.IsNullOrWhiteSpace(newItemName)) return;

                // Prompt for new DatabaseId base
                string newDbIdBase = ShowInputDialog(
                    "Clone Item — New DatabaseId Base",
                    $"Source: {sourceDbIdBase}-0001 through -{sourceEntries.Count:D4}\n\n" +
                    "New DatabaseId base (e.g., MDSK_JOINT_000128):",
                    "");

                if (string.IsNullOrWhiteSpace(newDbIdBase)) return;

                // Check target files
                string targetItmPath = Path.Combine(sourceDir, newItemName + ".itm");
                string targetTxtPath = Path.Combine(sourceDir, newItemName + ".Txt");
                if (File.Exists(targetItmPath) || File.Exists(targetTxtPath))
                {
                    if (MessageBox.Show($"Target files already exist in:\n{sourceDir}\n\nOverwrite?",
                        "Clone Item", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                // Clone the .itm
                var saveResult = ContentManager.SaveItemAs(_currentContentItem, sourceDir, newItemName, true);
                if (saveResult.Status != ResultStatus.Succeeded)
                {
                    MessageBox.Show($"Failed to save cloned item: {saveResult.Status}",
                        "Clone Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Generate new product list .Txt
                if (sourceEntries.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Name,DIM1,Order,ID");
                    for (int i = 0; i < sourceEntries.Count; i++)
                    {
                        string newDbId = $"{newDbIdBase}-{(i + 1):D4}";
                        sb.AppendLine($"{sourceEntries[i][0].Trim()},{sourceEntries[i][1].Trim()},{sourceEntries[i][2].Trim()},{newDbId}");
                    }
                    File.WriteAllText(targetTxtPath, sb.ToString());
                }

                // Copy .png icon if it exists
                if (File.Exists(sourcePngPath))
                {
                    string targetPngPath = Path.Combine(sourceDir, newItemName + ".png");
                    // Only copy if pre-cloned icon doesn't already exist
                    if (!File.Exists(targetPngPath))
                        File.Copy(sourcePngPath, targetPngPath, true);
                }

                MessageBox.Show(
                    $"Clone complete!\n\n" +
                    $"Created: {newItemName}.itm\n" +
                    $"Product list: {sourceEntries.Count} entries\n" +
                    $"DatabaseIds: {newDbIdBase}-0001 through -{sourceEntries.Count:D4}\n\n" +
                    "Refresh the item folder to see the new item.",
                    "Clone Item", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh the folder view
                if (_currentFolder != null)
                    OnContentFolderSelected(_currentFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Clone error: {ex.Message}", "Clone Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Simple WinForms input dialog for clone prompts.
        /// </summary>
        private string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using (var form = new System.Windows.Forms.Form())
            {
                form.Text = title;
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Width = 450;
                form.Height = 220;

                var label = new System.Windows.Forms.Label { Left = 12, Top = 12, Width = 410, Height = 100, Text = prompt };
                var textBox = new System.Windows.Forms.TextBox { Left = 12, Top = 115, Width = 410, Text = defaultValue ?? "" };
                var okBtn = new System.Windows.Forms.Button { Text = "OK", Left = 260, Top = 148, Width = 75, DialogResult = System.Windows.Forms.DialogResult.OK };
                var cancelBtn = new System.Windows.Forms.Button { Text = "Cancel", Left = 345, Top = 148, Width = 75, DialogResult = System.Windows.Forms.DialogResult.Cancel };

                form.Controls.AddRange(new System.Windows.Forms.Control[] { label, textBox, okBtn, cancelBtn });
                form.AcceptButton = okBtn;
                form.CancelButton = cancelBtn;

                return form.ShowDialog() == System.Windows.Forms.DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }

        #region Product List

        private void dgFolderContents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Product list data is loaded on double-click (full item detail view).
            // Keep panel hidden on single click to avoid unsafe API calls from event context.
            pnlProductInfo.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Context Menu

        private void dgFolderContents_RowRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var row = sender as DataGridRow;
            if (row == null) return;

            // Select the row on right-click
            dgFolderContents.SelectedItem = row.Item;
            var item = row.Item as FolderContentItem;
            if (item == null) return;

            var menu = new ContextMenu();

            if (item.IsFolder && item.Folder != null)
            {
                var openFolder = new MenuItem { Header = "Open Folder" };
                openFolder.Click += (s, args) => OnContentFolderSelected(item.Folder);
                menu.Items.Add(openFolder);

                var expandAll = new MenuItem { Header = "Expand All Items" };
                expandAll.Click += (s, args) => ShowRecursiveFolderContents(item.Folder);
                menu.Items.Add(expandAll);
            }
            else if (!item.IsFolder)
            {
                var openItem = new MenuItem { Header = "Open Item" };
                openItem.Click += (s, args) =>
                {
                    FabricationManager.CurrentLoadedItemPath = item.FullPath;
                    OnContentItemSelected(item.FullPath);
                };
                menu.Items.Add(openItem);

                var addToJob = new MenuItem { Header = "Add to Job" };
                addToJob.Click += (s, args) =>
                {
                    try
                    {
                        Item itm = ContentManager.LoadItem(item.FullPath);
                        if (itm != null)
                        {
                            FabricationManager.CurrentItem = itm;
                            FabricationAPIExamples.AddItemToJob(itm);
                        }
                    }
                    catch { }
                };
                menu.Items.Add(addToJob);
            }

            menu.Items.Add(new Separator());

            var selectAllInFolder = new MenuItem { Header = "Select All Items" };
            selectAllInFolder.Click += (s, args) => { btnSelectAll_Click(s, args); };
            menu.Items.Add(selectAllInFolder);

            var deselectAll = new MenuItem { Header = "Deselect All" };
            deselectAll.Click += (s, args) => { btnSelectNone_Click(s, args); };
            menu.Items.Add(deselectAll);

            row.ContextMenu = menu;
        }

        #endregion

        #region Recursive Folder Display

        private void tglShowSubfolders_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentFolder != null)
                ShowRecursiveFolderContents(_currentFolder);
        }

        private void tglShowSubfolders_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_currentFolder != null)
                OnContentFolderSelected(_currentFolder);
        }

        void ShowRecursiveFolderContents(ItemFolder rootFolder)
        {
            _currentFolder = rootFolder;
            _currentContentItem = null;
            pnlContentDetail.Visibility = Visibility.Collapsed;
            PopulateBulkCombos();

            _folderContents = new ObservableCollection<FolderContentItem>();
            CollectFolderContentsRecursive(rootFolder, 0);

            txtFolderPath.Text = rootFolder.Name + " (all subfolders)";
            dgFolderContents.ItemsSource = _folderContents;

            // Group by ParentPath then apply any active blank filter
            var view = CollectionViewSource.GetDefaultView(_folderContents);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription("ParentPath"));
            ApplyBlankFilter();

            pnlFolderContents.Visibility = Visibility.Visible;
        }

        void CollectFolderContentsRecursive(ItemFolder folder, int depth)
        {
            // Add .itm files in this folder
            if (Directory.Exists(folder.Directory))
            {
                foreach (string f in Directory.GetFiles(folder.Directory, "*.itm", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                {
                    var item = new FolderContentItem
                    {
                        IsFolder = false,
                        Name = Path.GetFileNameWithoutExtension(f),
                        FullPath = f,
                        TypeIcon = "\U0001F527",
                        IndentLevel = depth,
                        ParentPath = folder.Name
                    };

                    try
                    {
                        Item itm = ContentManager.LoadItem(f);
                        if (itm != null)
                        {
                            item.CID = itm.CID.ToString();
                            item.ServiceName = itm.Service?.Name ?? "";
                            item.MaterialName = itm.Material?.Name ?? "";
                            item.SpecificationName = itm.Specification?.Name ?? "";
                            item.PriceListName = itm.PriceList?.Name ?? "";
                            item.InstallTimesName = itm.InstallationTimesTable?.Name ?? "";
                            item.FabTimesName = itm.FabricationTimesTable?.Name ?? "";
                            item.IsBoughtOut = itm.BoughtOut;
                        }
                    }
                    catch { }

                    _folderContents.Add(item);
                }
            }

            // Recurse into subfolders
            if (folder.SubFolders != null)
            {
                foreach (var sub in folder.SubFolders.OrderBy(f => f.Name))
                {
                    CollectFolderContentsRecursive(sub, depth + 1);
                }
            }
        }

        #endregion

        #region Search

        private void txtSearchContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FabricationManager.ItemFoldersView == null) return;

            string filter = txtSearchContent.Text.Trim();
            var treeView = FabricationManager.ItemFoldersView.trvItemFolders;

            if (string.IsNullOrEmpty(filter))
            {
                // Restore all visibility
                SetAllTreeItemsVisible(treeView.Items);
                return;
            }

            foreach (object obj in treeView.Items)
            {
                var tvi = obj as TreeViewItem;
                if (tvi != null)
                    FilterTreeViewItem(tvi, filter);
            }
        }

        private bool FilterTreeViewItem(TreeViewItem item, string filter)
        {
            bool hasMatch = false;

            // Check this item's name
            string name = GetTreeViewItemText(item);
            bool thisMatch = name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            // Check children
            foreach (object child in item.Items)
            {
                var childItem = child as TreeViewItem;
                if (childItem != null)
                {
                    if (FilterTreeViewItem(childItem, filter))
                        hasMatch = true;
                }
            }

            if (thisMatch || hasMatch)
            {
                item.Visibility = Visibility.Visible;
                if (hasMatch)
                    item.IsExpanded = true;
                return true;
            }
            else
            {
                item.Visibility = Visibility.Collapsed;
                return false;
            }
        }

        private string GetTreeViewItemText(TreeViewItem item)
        {
            var stack = item.Header as StackPanel;
            if (stack != null)
            {
                foreach (var child in stack.Children)
                {
                    var lbl = child as Label;
                    if (lbl != null)
                        return lbl.Content?.ToString() ?? "";
                }
            }
            return item.Header?.ToString() ?? "";
        }

        private void SetAllTreeItemsVisible(ItemCollection items)
        {
            foreach (object obj in items)
            {
                var tvi = obj as TreeViewItem;
                if (tvi != null)
                {
                    tvi.Visibility = Visibility.Visible;
                    SetAllTreeItemsVisible(tvi.Items);
                }
            }
        }

        private void btnClearContentSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearchContent.Text = "";
        }

        #endregion

        #endregion
    }

    /// <summary>
    /// Model class for folder contents DataGrid rows
    /// </summary>
    public class FolderContentItem : INotifyPropertyChanged
    {
        bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool IsFolder { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string CID { get; set; }
        public string ServiceName { get; set; }
        public string MaterialName { get; set; }
        public string SpecificationName { get; set; }
        public string PriceListName { get; set; }
        public string InstallTimesName { get; set; }
        public string FabTimesName { get; set; }
        public bool IsBoughtOut { get; set; }
        public string TypeIcon { get; set; }
        public ItemFolder Folder { get; set; }
        public int IndentLevel { get; set; }
        public string ParentPath { get; set; }
        public bool HasProductList { get; set; }
        public List<ProductListRowViewModel> ProductListRows { get; set; }

        public string DisplayName
        {
            get
            {
                if (IndentLevel > 0)
                    return new string(' ', IndentLevel * 2) + Name;
                return Name;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// View model for a single product list row displayed in the product info grid.
    /// </summary>
    public class ProductListRowViewModel
    {
        public string DatabaseId { get; }
        public string Name { get; }
        public string DimValues { get; }
        public bool BoughtOut { get; }

        public ProductListRowViewModel(ItemProductListDataRow row)
        {
            DatabaseId = row.DatabaseId ?? "";
            Name = row.Name ?? "";
            BoughtOut = row.BoughtOut ?? false;

            // Format all dimension name=value pairs
            var parts = new List<string>();
            if (row.Dimensions != null)
            {
                foreach (ItemProductListDimensionEntry dim in row.Dimensions)
                {
                    try
                    {
                        string dimName = dim.Definition?.Name ?? "";
                        string val = dim.Value.ToString("G6").TrimEnd('0').TrimEnd('.');
                        parts.Add(string.IsNullOrEmpty(dimName) ? val : $"{dimName}={val}");
                    }
                    catch { }
                }
            }
            DimValues = string.Join("  |  ", parts);
        }
    }
}
