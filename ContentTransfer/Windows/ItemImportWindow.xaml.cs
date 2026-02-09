using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;

using FabricationSample.ContentTransfer.Models;
using FabricationSample.ContentTransfer.Services;

using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.ContentTransfer.Windows
{
    public partial class ItemImportWindow : Window
    {
        private readonly ContentPackage _package;
        private readonly string _packageFolder;
        private readonly List<ItemImportResult> _validationResults;
        private readonly List<CheckBox> _itemCheckBoxes = new List<CheckBox>();

        // Track override ComboBoxes per item index, keyed by reference type
        private readonly Dictionary<int, Dictionary<string, ComboBox>> _overrideComboBoxes
            = new Dictionary<int, Dictionary<string, ComboBox>>();

        /// <summary>
        /// Indices of items the user selected for import.
        /// </summary>
        public List<int> SelectedIndices { get; private set; }

        /// <summary>
        /// The target item folder directory path.
        /// </summary>
        public string TargetFolderPath { get; private set; }

        /// <summary>
        /// Per-item reference overrides chosen by the user (keyed by package item index).
        /// </summary>
        public Dictionary<int, ReferenceOverrides> OverridesPerItem { get; private set; }

        public ItemImportWindow(ContentPackage package, string packageFolder, List<ItemImportResult> validationResults)
        {
            InitializeComponent();
            _package = package;
            _packageFolder = packageFolder;
            _validationResults = validationResults;
            SelectedIndices = new List<int>();
            OverridesPerItem = new Dictionary<int, ReferenceOverrides>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Show package info
            txtPackageInfo.Text = $"Source: {_packageFolder}\n" +
                $"Config: \"{_package.ConfigurationName}\" ({_package.Items.Count} item{(_package.Items.Count == 1 ? "" : "s")})";

            // Populate item list with validation status
            PopulateItemList();

            // Populate target folder combo
            PopulateTargetFolders();

            UpdateWarningSummary();
            UpdateImportButtonState();
        }

        private void PopulateItemList()
        {
            _itemCheckBoxes.Clear();
            _overrideComboBoxes.Clear();
            pnlItems.Children.Clear();

            for (int i = 0; i < _package.Items.Count; i++)
            {
                var item = _package.Items[i];
                var validation = i < _validationResults.Count ? _validationResults[i] : null;

                var itemPanel = CreateItemPanel(i, item, validation);
                pnlItems.Children.Add(itemPanel);

                // Separator
                if (i < _package.Items.Count - 1)
                {
                    pnlItems.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
                }
            }
        }

        private Border CreateItemPanel(int index, ExportedItem item, ItemImportResult validation)
        {
            var border = new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
            };

            var stack = new StackPanel();

            // Header row: checkbox + filename
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            var checkBox = new CheckBox
            {
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = index
            };
            checkBox.Checked += ItemCheckBox_Changed;
            checkBox.Unchecked += ItemCheckBox_Changed;
            _itemCheckBoxes.Add(checkBox);

            var fileLabel = new TextBlock
            {
                Text = item.FileName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            headerStack.Children.Add(checkBox);
            headerStack.Children.Add(fileLabel);
            stack.Children.Add(headerStack);

            // Reference details
            var refs = item.References;
            if (refs != null)
            {
                var warningSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (validation?.Warnings != null)
                {
                    foreach (var w in validation.Warnings)
                        warningSet.Add(w);
                }

                _overrideComboBoxes[index] = new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);

                AddReferenceRow(stack, index, "Service", refs.ServiceName, warningSet, true, null);
                AddReferenceRow(stack, index, "Material", refs.MaterialName, warningSet, false,
                    GetMaterialNames());
                AddReferenceRow(stack, index, "Specification", refs.SpecificationName, warningSet, false,
                    GetSpecificationNames());
                AddReferenceRow(stack, index, "Section", refs.SectionDescription, warningSet, false,
                    GetSectionDescriptions());
                AddReferenceRow(stack, index, "PriceList", refs.PriceListName, warningSet, false,
                    GetPriceListNames());
                AddReferenceRow(stack, index, "InstallationTimesTable", refs.InstallationTimesTableName, warningSet, false,
                    GetInstallationTimesTableNames());
                AddReferenceRow(stack, index, "FabricationTimesTable", refs.FabricationTimesTableName, warningSet, false,
                    GetFabricationTimesTableNames());
            }

            // Product list info
            if (item.IsProductList && item.ProductList != null)
            {
                var plText = new TextBlock
                {
                    Text = $"  Product List: {item.ProductList.Rows?.Count ?? 0} rows",
                    FontSize = 11,
                    Foreground = (SolidColorBrush)FindResource("TextBrush"),
                    Margin = new Thickness(24, 1, 0, 0)
                };
                stack.Children.Add(plText);
            }

            border.Child = stack;
            return border;
        }

        private void AddReferenceRow(StackPanel parent, int itemIndex, string refKey, string value,
            HashSet<string> warnings, bool reportOnly, List<string> availableNames)
        {
            if (string.IsNullOrEmpty(value))
                return;

            // Check if any warning mentions this value
            bool hasWarning = warnings.Any(w => w.Contains(value));

            // Display-friendly label
            string displayLabel = GetDisplayLabel(refKey);

            if (!hasWarning)
            {
                // Matched - show green status
                var text = new TextBlock
                {
                    FontSize = 11,
                    Margin = new Thickness(24, 1, 0, 0)
                };
                text.Inlines.Add(new System.Windows.Documents.Run($"  {displayLabel}: {value}  ")
                {
                    Foreground = (SolidColorBrush)FindResource("TextBrush")
                });
                text.Inlines.Add(new System.Windows.Documents.Run("(ok)")
                {
                    Foreground = (SolidColorBrush)FindResource("SuccessBrush")
                });
                parent.Children.Add(text);
            }
            else if (reportOnly)
            {
                // Service - report only, no ComboBox
                var text = new TextBlock
                {
                    FontSize = 11,
                    Margin = new Thickness(24, 1, 0, 0)
                };
                text.Inlines.Add(new System.Windows.Documents.Run($"  {displayLabel}: {value}  ")
                {
                    Foreground = (SolidColorBrush)FindResource("TextBrush")
                });
                text.Inlines.Add(new System.Windows.Documents.Run("(!) not found (report-only)")
                {
                    Foreground = (SolidColorBrush)FindResource("WarningBrush"),
                    FontWeight = FontWeights.SemiBold
                });
                parent.Children.Add(text);
            }
            else
            {
                // Not matched - show warning label + ComboBox for reassignment
                var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 2, 0, 2) };

                var labelText = new TextBlock
                {
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                labelText.Inlines.Add(new System.Windows.Documents.Run($"  {displayLabel}: ")
                {
                    Foreground = (SolidColorBrush)FindResource("TextBrush")
                });
                labelText.Inlines.Add(new System.Windows.Documents.Run($"{value} (!) ")
                {
                    Foreground = (SolidColorBrush)FindResource("WarningBrush"),
                    FontWeight = FontWeights.SemiBold
                });

                rowPanel.Children.Add(labelText);

                // ComboBox with available names from the target database
                var combo = new ComboBox
                {
                    Width = 180,
                    Height = 22,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };

                // First item is "(skip)" to leave unresolved
                combo.Items.Add(new ComboBoxItem { Content = "(skip - leave unresolved)", Tag = "" });

                if (availableNames != null)
                {
                    foreach (var name in availableNames)
                    {
                        combo.Items.Add(new ComboBoxItem { Content = name, Tag = name });
                    }
                }

                combo.SelectedIndex = 0; // default to skip
                rowPanel.Children.Add(combo);

                parent.Children.Add(rowPanel);

                // Track this ComboBox for later retrieval
                _overrideComboBoxes[itemIndex][refKey] = combo;
            }
        }

        private string GetDisplayLabel(string refKey)
        {
            switch (refKey)
            {
                case "Material": return "Material";
                case "Specification": return "Specification";
                case "Section": return "Section";
                case "PriceList": return "Price List";
                case "InstallationTimesTable": return "Install Times";
                case "FabricationTimesTable": return "Fab Times";
                case "Service": return "Service";
                default: return refKey;
            }
        }

        #region Database Name Lookups

        private List<string> GetMaterialNames()
        {
            try { return FabDB.Materials.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        private List<string> GetSpecificationNames()
        {
            try { return FabDB.Specifications.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        private List<string> GetSectionDescriptions()
        {
            try { return FabDB.Sections.Select(s => s.Description).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        private List<string> GetPriceListNames()
        {
            try
            {
                return FabDB.SupplierGroups
                    .SelectMany(sg => sg.PriceLists)
                    .Select(pl => pl.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderBy(n => n)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private List<string> GetInstallationTimesTableNames()
        {
            try { return FabDB.InstallationTimesTable.Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        private List<string> GetFabricationTimesTableNames()
        {
            try { return FabDB.FabricationTimesTable.Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        #endregion

        private void PopulateTargetFolders()
        {
            try
            {
                var folders = ItemFolders.Folders;
                PopulateFolderCombo(folders, "");
            }
            catch { }
        }

        private void PopulateFolderCombo(IEnumerable<ItemFolder> folders, string prefix)
        {
            foreach (var folder in folders.OrderBy(f => f.Name))
            {
                string displayName = string.IsNullOrEmpty(prefix) ? folder.Name : $"{prefix} > {folder.Name}";
                cboTargetFolder.Items.Add(new ComboBoxItem
                {
                    Content = displayName,
                    Tag = folder
                });

                // Recurse into sub-folders
                if (folder.SubFolders != null && folder.SubFolders.Count > 0)
                {
                    PopulateFolderCombo(folder.SubFolders, displayName);
                }
            }
        }

        #region Event Handlers

        private void ItemCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateImportButtonState();
        }

        private void cboTargetFolder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateImportButtonState();
        }

        private void btnBrowseTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select target item folder for import";
                try
                {
                    var folders = ItemFolders.Folders;
                    if (folders.Count > 0)
                        dialog.SelectedPath = Path.GetDirectoryName(folders[0].Directory);
                }
                catch { }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var customItem = new ComboBoxItem
                    {
                        Content = dialog.SelectedPath,
                        Tag = dialog.SelectedPath
                    };
                    cboTargetFolder.Items.Add(customItem);
                    cboTargetFolder.SelectedItem = customItem;
                }
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            SelectedIndices = new List<int>();
            for (int i = 0; i < _itemCheckBoxes.Count; i++)
            {
                if (_itemCheckBoxes[i].IsChecked == true)
                    SelectedIndices.Add(i);
            }

            // Resolve target folder path
            var selectedComboItem = cboTargetFolder.SelectedItem as ComboBoxItem;
            if (selectedComboItem?.Tag is ItemFolder folder)
            {
                TargetFolderPath = folder.Directory;
            }
            else if (selectedComboItem?.Tag is string path)
            {
                TargetFolderPath = path;
            }

            if (SelectedIndices.Count == 0)
            {
                MessageBox.Show("No items selected.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(TargetFolderPath))
            {
                MessageBox.Show("Please select a target folder.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Collect overrides from ComboBoxes
            CollectOverrides();

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        private void CollectOverrides()
        {
            OverridesPerItem = new Dictionary<int, ReferenceOverrides>();

            foreach (var kvp in _overrideComboBoxes)
            {
                int itemIndex = kvp.Key;
                var combos = kvp.Value;

                if (combos.Count == 0)
                    continue;

                var overrides = new ReferenceOverrides();
                bool hasAny = false;

                foreach (var entry in combos)
                {
                    string refKey = entry.Key;
                    var combo = entry.Value;
                    var selected = combo.SelectedItem as ComboBoxItem;
                    string selectedName = selected?.Tag as string;

                    if (!string.IsNullOrEmpty(selectedName))
                    {
                        overrides.Overrides[refKey] = selectedName;
                        hasAny = true;
                    }
                }

                if (hasAny)
                    OverridesPerItem[itemIndex] = overrides;
            }
        }

        private void UpdateWarningSummary()
        {
            int totalWarnings = _validationResults.Sum(r => r.Warnings.Count);
            if (totalWarnings > 0)
            {
                txtWarningSummary.Text = $"(!) {totalWarnings} reference{(totalWarnings == 1 ? "" : "s")} not found in target config. Use drop-downs above to assign replacements.";
            }
            else
            {
                txtWarningSummary.Text = "";
            }
        }

        private void UpdateImportButtonState()
        {
            bool hasSelection = _itemCheckBoxes.Any(cb => cb.IsChecked == true);
            bool hasTarget = cboTargetFolder.SelectedItem != null;
            btnImport.IsEnabled = hasSelection && hasTarget;
        }
    }
}
