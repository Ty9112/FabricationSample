using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using Autodesk.Fabrication.DB;
using FabricationSample.Services.Export;
using FabricationSample.Services.Import;
using FabricationSample.Utilities;
using MessageBox = System.Windows.MessageBox;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Commands tab functionality
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        #region Command Descriptor

        private class CommandDescriptor : INotifyPropertyChanged
        {
            private bool _isSelected;

            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public bool IsEnabled { get; set; } = true;
            public string DisabledReason { get; set; }
            public Func<string> Execute { get; set; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        #endregion

        #region Commands Tab

        private ObservableCollection<CommandDescriptor> _commands;

        private void tbiCommands_Loaded(object sender, RoutedEventArgs e)
        {
            if (_commands != null)
                return;

            _commands = new ObservableCollection<CommandDescriptor>();

            // Export commands
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Product Info",
                Description = "Full product export with prices, labor, supplier IDs",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetProductInfo
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Export Item Data",
                Description = "Service items with product list entries and conditions",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteExportItemData
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Price Tables",
                Description = "Price lists and breakpoint tables (multi-file)",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetPriceTables
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Installation Times",
                Description = "Installation times tables (multi-file)",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetInstallationTimes
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Item Labor",
                Description = "Items with calculated labor from breakpoint tables",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetItemLabor
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Item Installation Tables",
                Description = "Items with assigned installation table mappings",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetItemInstallationTables
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Get Service Template Data",
                Description = "Service template buttons, codes, and item paths",
                Category = "Export",
                IsSelected = false,
                Execute = ExecuteGetServiceTemplateData
            });

            // Import commands
            _commands.Add(new CommandDescriptor
            {
                Name = "Import Installation Times",
                Description = "Installation times from CSV",
                Category = "Import",
                IsSelected = false,
                Execute = ExecuteImportInstallationTimes
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Import Product Database",
                Description = "Product definitions and supplier IDs from CSV",
                Category = "Import",
                IsSelected = false,
                Execute = ExecuteImportProductDatabase
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Import Supplier Discounts",
                Description = "Discount codes from CSV",
                Category = "Import",
                IsSelected = false,
                Execute = ExecuteImportSupplierDiscounts
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Import Button Report",
                Description = "Service template button codes from CSV",
                Category = "Import",
                IsSelected = false,
                Execute = ExecuteImportButtonReport
            });
            _commands.Add(new CommandDescriptor
            {
                Name = "Import Price List",
                Description = "Price data into a selected price list",
                Category = "Import",
                IsEnabled = false,
                DisabledReason = "Requires price list selection on Price Lists tab",
                IsSelected = false,
                Execute = null
            });

            lstCommands.ItemsSource = _commands;

            // Set up grouping by Category
            var view = CollectionViewSource.GetDefaultView(_commands);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription("Category"));

            UpdateCommandSummary();
        }

        private void btnCommandSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_commands == null) return;
            foreach (var cmd in _commands.Where(c => c.IsEnabled))
                cmd.IsSelected = true;
            UpdateCommandSummary();
        }

        private void btnCommandSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (_commands == null) return;
            foreach (var cmd in _commands)
                cmd.IsSelected = false;
            UpdateCommandSummary();
        }

        private void btnCommandSelectExports_Click(object sender, RoutedEventArgs e)
        {
            if (_commands == null) return;
            foreach (var cmd in _commands)
                cmd.IsSelected = cmd.IsEnabled && cmd.Category == "Export";
            UpdateCommandSummary();
        }

        private void btnCommandSelectImports_Click(object sender, RoutedEventArgs e)
        {
            if (_commands == null) return;
            foreach (var cmd in _commands)
                cmd.IsSelected = cmd.IsEnabled && cmd.Category == "Import";
            UpdateCommandSummary();
        }

        private void CommandCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateCommandSummary();
        }

        private void UpdateCommandSummary()
        {
            if (_commands == null || txtCommandSummary == null) return;
            int selected = _commands.Count(c => c.IsSelected && c.IsEnabled);
            txtCommandSummary.Text = $"{selected} command(s) selected";
        }

        private async void btnRunSelectedCommands_Click(object sender, RoutedEventArgs e)
        {
            if (_commands == null) return;

            var selectedCommands = _commands.Where(c => c.IsSelected && c.IsEnabled && c.Execute != null).ToList();

            if (selectedCommands.Count == 0)
            {
                MessageBox.Show("No commands selected.", "Commands", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirmation
            string commandList = string.Join("\n", selectedCommands.Select(c => $"  - {c.Name}"));
            var confirm = MessageBox.Show(
                $"Run {selectedCommands.Count} command(s)?\n\n{commandList}",
                "Confirm Run", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            // Disable UI during execution
            btnRunSelectedCommands.IsEnabled = false;
            prgCommands.Maximum = selectedCommands.Count;
            prgCommands.Value = 0;

            var results = new List<string>();
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < selectedCommands.Count; i++)
            {
                var cmd = selectedCommands[i];
                txtCommandStatus.Text = $"Running ({i + 1}/{selectedCommands.Count}): {cmd.Name}...";
                prgCommands.Value = i;

                try
                {
                    // Execute the command - returns a result message
                    string result = cmd.Execute();
                    if (result != null && result.StartsWith("ERROR:"))
                    {
                        results.Add($"[FAIL] {cmd.Name}: {result.Substring(6).Trim()}");
                        failCount++;
                    }
                    else
                    {
                        results.Add($"[OK] {cmd.Name}: {result ?? "Completed"}");
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"[FAIL] {cmd.Name}: {ex.Message}");
                    failCount++;
                }

                // Allow UI to update
                await System.Threading.Tasks.Task.Delay(50);
            }

            prgCommands.Value = selectedCommands.Count;
            txtCommandStatus.Text = $"Complete: {successCount} succeeded, {failCount} failed";

            // Show summary
            string summary = $"Commands completed: {successCount} succeeded, {failCount} failed\n\n" +
                           string.Join("\n", results);
            MessageBox.Show(summary, "Commands Complete", MessageBoxButton.OK,
                failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // Reset
            btnRunSelectedCommands.IsEnabled = true;
            prgCommands.Value = 0;
        }

        #endregion

        #region Export Command Implementations

        /// <summary>
        /// Exports to a temp file, shows preview, then lets user Save As.
        /// Returns a result message string.
        /// </summary>
        private string ExportWithPreview(string commandName, string defaultFileName, Func<string, ExportResult> exportAction)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"FabExport_{Guid.NewGuid():N}.csv");

            try
            {
                var result = exportAction(tempPath);

                if (result == null)
                    return "ERROR: Export returned null";
                if (result.WasCancelled)
                    return "Cancelled";
                if (!result.IsSuccess)
                    return $"ERROR: {result.ErrorMessage}";

                // Show preview window
                var previewWindow = new ExportPreviewWindow(tempPath, commandName, defaultFileName);
                previewWindow.ShowDialog();

                if (!previewWindow.DialogResultOk || string.IsNullOrEmpty(previewWindow.SavePath))
                {
                    // Cleanup handled by preview window on cancel
                    return "Cancelled";
                }

                // Copy temp file to user-chosen location
                File.Copy(tempPath, previewWindow.SavePath, true);

                // Cleanup temp
                try { File.Delete(tempPath); } catch { }

                return $"{result.RecordCount} records exported to {Path.GetFileName(previewWindow.SavePath)}";
            }
            catch (Exception ex)
            {
                try { File.Delete(tempPath); } catch { }
                return $"ERROR: {ex.Message}";
            }
        }

        private string ExecuteGetProductInfo()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return ExportWithPreview("Get Product Info", $"ProductInfo_{timestamp}.csv",
                tempPath =>
                {
                    var service = new ProductInfoExportService();
                    return service.Export(tempPath);
                });
        }

        private string ExecuteExportItemData()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return ExportWithPreview("Export Item Data", $"ItemData_{timestamp}.csv",
                tempPath =>
                {
                    var service = new ItemDataExportService();
                    return service.Export(tempPath);
                });
        }

        private string ExecuteGetPriceTables()
        {
            // Show price table selection dialog
            var selectionWindow = new PriceTableSelectionWindow();
            selectionWindow.ShowDialog();

            if (!selectionWindow.DialogResultOk)
                return "Cancelled";

            var selectedTables = selectionWindow.SelectedPriceTables;
            if (selectedTables == null || selectedTables.Count == 0)
                return "No tables selected";

            // Multi-file export — export to temp folder, preview first file, then Save As to final folder
            string tempFolder = Path.Combine(Path.GetTempPath(), $"FabExport_{Guid.NewGuid():N}");

            try
            {
                var service = new PriceTablesExportService { SelectedTables = selectedTables };
                var result = service.Export(tempFolder);

                if (result is PriceTablesExportResult ptResult && ptResult.IsSuccess)
                {
                    // Find first CSV in temp folder for preview
                    string firstCsv = Directory.GetFiles(tempFolder, "*.csv").FirstOrDefault();
                    if (firstCsv != null)
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var previewWindow = new ExportPreviewWindow(firstCsv,
                            $"Price Tables ({ptResult.FileCount} files)",
                            $"PriceTables_{timestamp}");
                        previewWindow.ShowDialog();

                        if (!previewWindow.DialogResultOk || string.IsNullOrEmpty(previewWindow.SavePath))
                        {
                            try { Directory.Delete(tempFolder, true); } catch { }
                            return "Cancelled";
                        }

                        // Use the chosen path's directory as target folder
                        string targetFolder = Path.GetDirectoryName(previewWindow.SavePath);
                        string finalFolder = Path.Combine(targetFolder, $"PriceTables_{timestamp}");
                        CopyDirectory(tempFolder, finalFolder);
                        try { Directory.Delete(tempFolder, true); } catch { }

                        return $"{ptResult.FileCount} files exported to {Path.GetFileName(finalFolder)}";
                    }

                    try { Directory.Delete(tempFolder, true); } catch { }
                    return $"{ptResult.FileCount} files exported (no preview available)";
                }
                else
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                    return $"ERROR: {result?.ErrorMessage ?? "Unknown error"}";
                }
            }
            catch (Exception ex)
            {
                try { Directory.Delete(tempFolder, true); } catch { }
                return $"ERROR: {ex.Message}";
            }
        }

        private string ExecuteGetInstallationTimes()
        {
            // Show installation table selection dialog
            var selectionWindow = new InstallTableSelectionWindow();
            selectionWindow.ShowDialog();

            if (!selectionWindow.DialogResultOk)
                return "Cancelled";

            var selectedTables = selectionWindow.SelectedInstallTables;
            if (selectedTables == null || selectedTables.Count == 0)
                return "No tables selected";

            // Multi-file export — export to temp folder, preview first file, then Save As to final folder
            string tempFolder = Path.Combine(Path.GetTempPath(), $"FabExport_{Guid.NewGuid():N}");

            try
            {
                var service = new InstallationTimesExportService { SelectedTables = selectedTables };
                var result = service.Export(tempFolder);

                if (result is InstallationTimesExportResult itResult && itResult.IsSuccess)
                {
                    string firstCsv = Directory.GetFiles(tempFolder, "*.csv").FirstOrDefault();
                    if (firstCsv != null)
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var previewWindow = new ExportPreviewWindow(firstCsv,
                            $"Installation Times ({itResult.FileCount} files)",
                            $"InstallationTimes_{timestamp}");
                        previewWindow.ShowDialog();

                        if (!previewWindow.DialogResultOk || string.IsNullOrEmpty(previewWindow.SavePath))
                        {
                            try { Directory.Delete(tempFolder, true); } catch { }
                            return "Cancelled";
                        }

                        string targetFolder = Path.GetDirectoryName(previewWindow.SavePath);
                        string finalFolder = Path.Combine(targetFolder, $"InstallationTimes_{timestamp}");
                        CopyDirectory(tempFolder, finalFolder);
                        try { Directory.Delete(tempFolder, true); } catch { }

                        return $"{itResult.FileCount} files exported to {Path.GetFileName(finalFolder)}";
                    }

                    try { Directory.Delete(tempFolder, true); } catch { }
                    return $"{itResult.FileCount} files exported (no preview available)";
                }
                else
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                    return $"ERROR: {result?.ErrorMessage ?? "Unknown error"}";
                }
            }
            catch (Exception ex)
            {
                try { Directory.Delete(tempFolder, true); } catch { }
                return $"ERROR: {ex.Message}";
            }
        }

        private string ExecuteGetItemLabor()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return ExportWithPreview("Get Item Labor", $"ItemLabor_{timestamp}.csv",
                tempPath =>
                {
                    var service = new ItemLaborExportService();
                    return service.Export(tempPath);
                });
        }

        private string ExecuteGetItemInstallationTables()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return ExportWithPreview("Get Item Installation Tables", $"ItemInstallationTables_{timestamp}.csv",
                tempPath =>
                {
                    var service = new ItemInstallationTablesExportService();
                    return service.Export(tempPath);
                });
        }

        private string ExecuteGetServiceTemplateData()
        {
            // Show service selection dialog
            var selectionWindow = new ServiceSelectionWindow();
            selectionWindow.ShowDialog();

            if (!selectionWindow.DialogResultOk)
                return "Cancelled";

            var selectedServices = selectionWindow.SelectedServiceNames;
            if (selectedServices == null || selectedServices.Count == 0)
                return "No services selected";

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return ExportWithPreview("Service Template Data", $"ServiceTemplateData_{timestamp}.csv",
                tempPath =>
                {
                    var service = new ServiceTemplateDataExportService { SelectedServiceNames = selectedServices };
                    var options = new ExportOptions { IncludeHeader = true };
                    return service.Export(tempPath, options);
                });
        }

        /// <summary>
        /// Copies all files from source directory to target directory.
        /// </summary>
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
        }

        #endregion

        #region Import Command Implementations

        private string ExecuteImportInstallationTimes()
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Select InstallationProducts CSV file to import";
                fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                fileDialog.CheckFileExists = true;

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return "Cancelled";

                string importFile = fileDialog.FileName;

                var requiredFields = new[] { "TableName", "Id", "LaborRate" };
                var optionalFields = new[] { "TableGroup", "Units", "Status" };

                var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk)
                    return "Cancelled";

                var options = new ImportOptions
                {
                    HasHeaderRow = true,
                    UpdateExisting = true,
                    StopOnFirstError = false
                };
                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new InstallationTimesImportService();

                var validation = importService.Validate(importFile, options);
                if (!validation.IsValid)
                    return $"ERROR: Validation failed with {validation.Errors.Count} error(s)";

                var preview = importService.Preview(importFile, options);
                if (!preview.IsSuccess)
                    return $"ERROR: Preview failed: {preview.ErrorMessage}";

                var confirmMsg = $"Import {preview.UpdatedRecordCount} updated, {preview.NewRecordCount} new records?";
                if (MessageBox.Show(confirmMsg, "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return "Cancelled";

                var result = importService.Import(importFile, options);

                if (result.IsSuccess)
                    return $"Imported {result.ImportedCount} records (skipped {result.SkippedCount})";
                else
                    return $"ERROR: {result.ErrorMessage}";
            }
        }

        private string ExecuteImportProductDatabase()
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Select Product Database CSV file to import";
                fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                fileDialog.CheckFileExists = true;

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return "Cancelled";

                string importFile = fileDialog.FileName;

                var requiredFields = new[] { "Id" };
                var optionalFields = new List<string>
                {
                    "Description", "Finish", "Specification", "Material", "ProductName",
                    "Range", "Size", "Manufacturer", "Source", "InstallType", "Group"
                };

                var supplierNames = ProductDatabase.Suppliers.Select(s => s.Name).ToList();
                optionalFields.AddRange(supplierNames);

                var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields.ToArray());
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk)
                    return "Cancelled";

                var options = new ImportOptions
                {
                    HasHeaderRow = true,
                    UpdateExisting = true,
                    StopOnFirstError = false
                };
                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new ProductDatabaseImportService { SupplierColumns = supplierNames };

                var validation = importService.Validate(importFile, options);
                if (!validation.IsValid)
                    return $"ERROR: Validation failed with {validation.Errors.Count} error(s)";

                var preview = importService.Preview(importFile, options);
                if (!preview.IsSuccess)
                    return $"ERROR: Preview failed: {preview.ErrorMessage}";

                var confirmMsg = $"Import {preview.UpdatedRecordCount} updated, {preview.NewRecordCount} new products?";
                if (MessageBox.Show(confirmMsg, "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return "Cancelled";

                var result = importService.Import(importFile, options);

                if (result.IsSuccess)
                    return $"Imported {result.ImportedCount} products (skipped {result.SkippedCount})";
                else
                    return $"ERROR: {result.ErrorMessage}";
            }
        }

        private string ExecuteImportSupplierDiscounts()
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Select Supplier Discounts CSV file to import";
                fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                fileDialog.CheckFileExists = true;

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return "Cancelled";

                string importFile = fileDialog.FileName;

                var requiredFields = new[] { "SupplierGroup", "Code" };
                var optionalFields = new[] { "Value", "Description" };

                var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk)
                    return "Cancelled";

                var options = new ImportOptions
                {
                    HasHeaderRow = true,
                    UpdateExisting = true,
                    StopOnFirstError = false
                };
                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new SupplierDiscountsImportService();

                var validation = importService.Validate(importFile, options);
                if (!validation.IsValid)
                    return $"ERROR: Validation failed with {validation.Errors.Count} error(s)";

                var preview = importService.Preview(importFile, options);
                if (!preview.IsSuccess)
                    return $"ERROR: Preview failed: {preview.ErrorMessage}";

                var confirmMsg = $"Import {preview.UpdatedRecordCount} updated, {preview.NewRecordCount} new discounts?";
                if (MessageBox.Show(confirmMsg, "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return "Cancelled";

                var result = importService.Import(importFile, options);

                if (result.IsSuccess)
                    return $"Imported {result.ImportedCount} discounts (skipped {result.SkippedCount})";
                else
                    return $"ERROR: {result.ErrorMessage}";
            }
        }

        private string ExecuteImportButtonReport()
        {
            using (var fileDialog = new OpenFileDialog())
            {
                fileDialog.Title = "Select Button Report CSV file to import";
                fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                fileDialog.CheckFileExists = true;

                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return "Cancelled";

                string importFile = fileDialog.FileName;

                var requiredFields = new[] { "Service Name", "Tab", "Name" };
                var optionalFields = new[] { "Template Name", "Button Code", "Item Path1", "Condition1", "Item Path2", "Condition2", "Item Path3", "Condition3", "Item Path4", "Condition4" };

                var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk)
                    return "Cancelled";

                var options = new ImportOptions
                {
                    HasHeaderRow = true,
                    UpdateExisting = true,
                    StopOnFirstError = false
                };
                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new ServiceTemplateDataImportService { MatchByTemplate = false };

                var validation = importService.Validate(importFile, options);
                if (!validation.IsValid)
                    return $"ERROR: Validation failed with {validation.Errors.Count} error(s)";

                var preview = importService.Preview(importFile, options);
                if (!preview.IsSuccess)
                    return $"ERROR: Preview failed: {preview.ErrorMessage}";

                var confirmMsg = $"Import {preview.UpdatedRecordCount} updated buttons?";
                if (MessageBox.Show(confirmMsg, "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return "Cancelled";

                var result = importService.Import(importFile, options);

                if (result.IsSuccess)
                    return $"Updated {result.ImportedCount} buttons (skipped {result.SkippedCount})";
                else
                    return $"ERROR: {result.ErrorMessage}";
            }
        }

        #endregion
    }
}
