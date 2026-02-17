using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using FabricationSample.Services.Import;
using MessageBox = System.Windows.MessageBox;
using Clipboard = System.Windows.Clipboard;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Import functionality
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        #region Import Button Handlers

        /// <summary>
        /// Import Price List button click handler.
        /// Prompts user for CSV file and imports price data into the currently selected price list.
        /// </summary>
        private void btnImportPriceList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate that a price list is selected
                if (_pl == null)
                {
                    MessageBox.Show(
                        "No price list selected.\n\nPlease select a price list before importing.",
                        "No Price List Selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Only support Product Id type price lists for now
                if (_pl.Type != TableType.ProductId)
                {
                    MessageBox.Show(
                        "Import is only supported for Product Id based price lists.\n\n" +
                        "Breakpoint table imports are not yet supported.",
                        "Unsupported Price List Type",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var priceList = _pl as PriceList;
                if (priceList == null)
                {
                    MessageBox.Show(
                        "Failed to cast price list to PriceList type.",
                        "Import Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Prompt for import file
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Show column mapping dialog
                    var requiredFields = new[] { "DatabaseId", "Cost" };
                    var optionalFields = new[] { "DiscountCode", "Units", "Status", "Date" };

                    var mappingWindow = new ColumnMappingWindow(
                        importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    // Create import options with column mapping
                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                    {
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;
                    }

                    // Create import service
                    var importService = new PriceTableImportService(priceList, _sg);

                    // Validate file with column mapping
                    var validation = importService.Validate(importFile, options);

                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                        {
                            errorMsg += $"  {error}\n";
                        }
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Show warnings if any
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                        {
                            warnMsg += $"  {warning}\n";
                        }
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";

                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Generate preview with column mapping
                    var preview = importService.Preview(importFile, options);

                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import price list data:\n\n" +
                                          $"New entries: {preview.NewRecordCount}\n" +
                                          $"Updated entries: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    // Perform import with column mapping
                    importService.ProgressChanged += (s, args) =>
                    {
                        // Update progress on UI thread
                        SafeInvoke(() =>
                        {
                            prgPriceList.Value = args.Percentage;
                        });
                    };

                    var result = importService.Import(importFile, options);

                    // Handle result
                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Imported: {result.ImportedCount} records\n";

                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} records\n";

                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} records\n";

                        successMsg += "\nSave database to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Refresh the price list display
                        RefreshPriceListDisplay();
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Import failed: {result.ErrorMessage}",
                            "Import Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }

                    // Reset progress bar
                    prgPriceList.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error during import: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Refresh the price list display after import.
        /// </summary>
        private void RefreshPriceListDisplay()
        {
            try
            {
                if (_pl == null || _pl.Type != TableType.ProductId)
                    return;

                var priceList = _pl as PriceList;
                if (priceList == null)
                    return;

                // Clear and reload the price list grid
                if (_lstPrices != null)
                {
                    _lstPrices.Clear();

                    foreach (ProductEntry prodEntry in priceList.Products)
                    {
                        var gridItem = new Data.ProductEntryGridItem(prodEntry, _sg);
                        _lstPrices.Add(gridItem);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error refreshing display: {ex.Message}",
                    "Refresh Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Import Installation Times button click handler.
        /// Prompts user for CSV file and imports installation times data.
        /// </summary>
        private void btnImportInstallationTimes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select InstallationProducts CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Show column mapping dialog
                    var requiredFields = new[] { "TableName", "Id", "LaborRate" };
                    var optionalFields = new[] { "TableGroup", "Units", "Status" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new InstallationTimesImportService();

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import installation times:\n\n" +
                                          $"New entries: {preview.NewRecordCount}\n" +
                                          $"Updated entries: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    importService.ProgressChanged += (s, args) =>
                    {
                        SafeInvoke(() => { prgInstallationTimes.Value = args.Percentage; });
                    };

                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Imported: {result.ImportedCount} records\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} records\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} records\n";
                        successMsg += "\nSave database to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    prgInstallationTimes.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Button Report for Services tab.
        /// Imports service template data (button codes) from CSV.
        /// </summary>
        private void btnImportButtonReport_Click(object sender, RoutedEventArgs e)
        {
            ImportServiceTemplateData(matchByTemplate: false);
        }

        /// <summary>
        /// Import Button Report for Service Templates tab.
        /// Imports service template data (button codes) from CSV by template name.
        /// </summary>
        private void btnImportTemplateButtonReport_Click(object sender, RoutedEventArgs e)
        {
            ImportServiceTemplateData(matchByTemplate: true);
        }

        /// <summary>
        /// Common handler for service template data import.
        /// </summary>
        private void ImportServiceTemplateData(bool matchByTemplate)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Button Report CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Determine required/optional fields based on mode
                    string[] requiredFields;
                    string[] optionalFields;

                    if (matchByTemplate)
                    {
                        requiredFields = new[] { "Template Name", "Tab", "Name" };
                        optionalFields = new[] { "Button Code", "Item Path1", "Condition1", "Item Path2", "Condition2", "Item Path3", "Condition3", "Item Path4", "Condition4" };
                    }
                    else
                    {
                        requiredFields = new[] { "Service Name", "Tab", "Name" };
                        optionalFields = new[] { "Template Name", "Button Code", "Item Path1", "Condition1", "Item Path2", "Condition2", "Item Path3", "Condition3", "Item Path4", "Condition4" };
                    }

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new ServiceTemplateDataImportService
                    {
                        MatchByTemplate = matchByTemplate
                    };

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import button report data:\n\n" +
                                          $"Updated buttons: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Updated: {result.ImportedCount} buttons\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} entries\n";
                        successMsg += "\nSave services/templates to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Product Database button click handler.
        /// Imports product definitions and supplier IDs from CSV, similar to productinformationeditor.exe (mapprod.exe).
        /// </summary>
        private void btnImportProductDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Product Database CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Build field lists - required + optional standard + supplier names
                    var requiredFields = new[] { "Id" };
                    var optionalFields = new List<string>
                    {
                        "Description", "Finish", "Specification", "Material", "ProductName",
                        "Range", "Size", "Manufacturer", "Source", "InstallType", "Group"
                    };

                    // Add supplier names as optional fields
                    var supplierNames = ProductDatabase.Suppliers.Select(s => s.Name).ToList();
                    optionalFields.AddRange(supplierNames);

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new ProductDatabaseImportService
                    {
                        SupplierColumns = supplierNames
                    };

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import product database:\n\n" +
                                          $"Updated products: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped products: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    importService.ProgressChanged += (s, args) =>
                    {
                        SafeInvoke(() => { prgProductDatabase.Value = args.Percentage; });
                    };

                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Updated: {result.ImportedCount} products\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} products\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} products\n";
                        successMsg += "\nClick 'Save Product Database' to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Reload the grid to show updated data
                        ReloadProductData();
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    prgProductDatabase.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Supplier Discounts button click handler.
        /// Imports discount codes, values, and descriptions from CSV.
        /// </summary>
        private void btnImportSupplierDiscounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Supplier Discounts CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Column mapping
                    var requiredFields = new[] { "SupplierGroup", "Code" };
                    var optionalFields = new[] { "Value", "Description" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new SupplierDiscountsImportService();

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import supplier discounts:\n\n" +
                                          $"Updated discounts: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped discounts: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    importService.ProgressChanged += (s, args) =>
                    {
                        SafeInvoke(() => { prgSupplierDiscounts.Value = args.Percentage; });
                    };

                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Updated: {result.ImportedCount} discounts\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} discounts\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} discounts\n";
                        successMsg += "\nClick 'Save Price Lists' to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Reload the discounts grid if a supplier group is selected
                        if (cmbSupplierGroups.SelectedItem is SupplierGroup sg)
                        {
                            dgSupplierDiscounts.ItemsSource = sg.Discounts?.Discounts;
                        }
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    prgSupplierDiscounts.Value = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Item Statuses from CSV.
        /// </summary>
        private void btnImportItemStatuses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Item Statuses CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Show column mapping dialog
                    var requiredFields = new[] { "Name" };
                    var optionalFields = new[] { "LayerTag", "Color", "Output" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new ItemStatusesImportService();

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import item statuses:\n\n" +
                                          $"New statuses: {preview.NewRecordCount}\n" +
                                          $"Updated statuses: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Imported: {result.ImportedCount} item statuses\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} statuses\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} statuses\n";
                        successMsg += "\nSave database to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Reload the item statuses grid
                        dgItemStatuses_Loaded(dgItemStatuses, null);
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Job Statuses from CSV.
        /// </summary>
        private void btnImportJobStatuses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Job Statuses CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Show column mapping dialog
                    var requiredFields = new[] { "Description" };
                    var optionalFields = new[] { "Active", "DoCopy", "CopyJobToFolder", "DoSave", "DoExport", "ExportFile", "DeActivateOnCompletion" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = mappingWindow.HasHeaders,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new JobStatusesImportService();

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Warnings
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import job statuses:\n\n" +
                                          $"New statuses: {preview.NewRecordCount}\n" +
                                          $"Updated statuses: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Imported: {result.ImportedCount} job statuses\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} statuses\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} statuses\n";
                        successMsg += "\nSave database to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Reload the job statuses grid
                        dgJobStatuses_Loaded(dgJobStatuses, null);
                    }
                    else if (result.WasCancelled)
                    {
                        MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Clipboard Paste Import

        /// <summary>
        /// Result of reading clipboard data for import.
        /// </summary>
        private class ClipboardImportData
        {
            public string TempFilePath { get; set; }
            public char Delimiter { get; set; }
        }

        /// <summary>
        /// Read clipboard text, detect delimiter, write to temp CSV file.
        /// Returns null if clipboard is empty or not text.
        /// ESTmep copies data as tab-separated, so tab is the primary delimiter.
        /// </summary>
        private ClipboardImportData GetClipboardAsImportFile()
        {
            string clipText;
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show(
                        "No text data found on clipboard.\n\nCopy data from ESTmep or a spreadsheet first.",
                        "Clipboard Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
                clipText = Clipboard.GetText();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read clipboard: {ex.Message}",
                    "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (string.IsNullOrWhiteSpace(clipText))
            {
                MessageBox.Show(
                    "Clipboard text is empty.\n\nCopy data from ESTmep or a spreadsheet first.",
                    "Clipboard Empty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            // Detect delimiter: count tabs vs commas in first line
            var firstLine = clipText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(firstLine))
                return null;

            int tabCount = firstLine.Count(c => c == '\t');
            int commaCount = firstLine.Count(c => c == ',');
            char delimiter = tabCount >= commaCount ? '\t' : ',';

            // Write to temp file
            string tempPath = Path.Combine(Path.GetTempPath(), $"FabImport_{Guid.NewGuid():N}.csv");
            try
            {
                File.WriteAllText(tempPath, clipText);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write temp file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            return new ClipboardImportData
            {
                TempFilePath = tempPath,
                Delimiter = delimiter
            };
        }

        /// <summary>
        /// Clean up temporary import file.
        /// </summary>
        private void CleanupTempFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { /* temp file cleanup is best-effort */ }
        }

        /// <summary>
        /// Paste Price List import from clipboard.
        /// </summary>
        private void btnPasteImportPriceList_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                if (_pl == null)
                {
                    MessageBox.Show(
                        "No price list selected.\n\nPlease select a price list before importing.",
                        "No Price List Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_pl.Type != TableType.ProductId)
                {
                    MessageBox.Show(
                        "Import is only supported for Product Id based price lists.\n\nBreakpoint table imports are not yet supported.",
                        "Unsupported Price List Type", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var priceList = _pl as PriceList;
                if (priceList == null) return;

                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "DatabaseId", "Cost" };
                var optionalFields = new[] { "DiscountCode", "Units", "Status", "Date" };

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new PriceTableImportService(priceList, _sg);

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import price list data (from clipboard):\n\n" +
                                      $"New entries: {preview.NewRecordCount}\n" +
                                      $"Updated entries: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                importService.ProgressChanged += (s, args) =>
                {
                    SafeInvoke(() => { prgPriceList.Value = args.Percentage; });
                };

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\n" +
                                      $"Imported: {result.ImportedCount} records\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} records\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} records\n";
                    successMsg += "\nSave database to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    RefreshPriceListDisplay();
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                prgPriceList.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Installation Times import from clipboard.
        /// </summary>
        private void btnPasteImportInstallationTimes_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "TableName", "Id", "LaborRate" };
                var optionalFields = new[] { "TableGroup", "Units", "Status" };

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new InstallationTimesImportService();

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import installation times (from clipboard):\n\n" +
                                      $"New entries: {preview.NewRecordCount}\n" +
                                      $"Updated entries: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                importService.ProgressChanged += (s, args) =>
                {
                    SafeInvoke(() => { prgInstallationTimes.Value = args.Percentage; });
                };

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nImported: {result.ImportedCount} records\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} records\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} records\n";
                    successMsg += "\nSave database to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                prgInstallationTimes.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Product Database import from clipboard.
        /// </summary>
        private void btnPasteImportProductDatabase_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "Id" };
                var optionalFields = new List<string>
                {
                    "Description", "Finish", "Specification", "Material", "ProductName",
                    "Range", "Size", "Manufacturer", "Source", "InstallType", "Group"
                };

                var supplierNames = ProductDatabase.Suppliers.Select(s => s.Name).ToList();
                optionalFields.AddRange(supplierNames);

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new ProductDatabaseImportService
                {
                    SupplierColumns = supplierNames
                };

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import product database (from clipboard):\n\n" +
                                      $"Updated products: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped products: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                importService.ProgressChanged += (s, args) =>
                {
                    SafeInvoke(() => { prgProductDatabase.Value = args.Percentage; });
                };

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nUpdated: {result.ImportedCount} products\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} products\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} products\n";
                    successMsg += "\nClick 'Save Product Database' to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    ReloadProductData();
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                prgProductDatabase.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Supplier Discounts import from clipboard.
        /// </summary>
        private void btnPasteImportSupplierDiscounts_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "SupplierGroup", "Code" };
                var optionalFields = new[] { "Value", "Description" };

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new SupplierDiscountsImportService();

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import supplier discounts (from clipboard):\n\n" +
                                      $"Updated discounts: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped discounts: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                importService.ProgressChanged += (s, args) =>
                {
                    SafeInvoke(() => { prgSupplierDiscounts.Value = args.Percentage; });
                };

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nUpdated: {result.ImportedCount} discounts\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} discounts\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} discounts\n";
                    successMsg += "\nClick 'Save Price Lists' to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                    if (cmbSupplierGroups.SelectedItem is SupplierGroup sg)
                        dgSupplierDiscounts.ItemsSource = sg.Discounts?.Discounts;
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                prgSupplierDiscounts.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Item Statuses import from clipboard.
        /// </summary>
        private void btnPasteImportItemStatuses_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "Name" };
                var optionalFields = new[] { "LayerTag", "Color", "Output" };

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new ItemStatusesImportService();

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import item statuses (from clipboard):\n\n" +
                                      $"New statuses: {preview.NewRecordCount}\n" +
                                      $"Updated statuses: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nImported: {result.ImportedCount} item statuses\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} statuses\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} statuses\n";
                    successMsg += "\nSave database to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    dgItemStatuses_Loaded(dgItemStatuses, null);
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Job Statuses import from clipboard.
        /// </summary>
        private void btnPasteImportJobStatuses_Click(object sender, RoutedEventArgs e)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                var requiredFields = new[] { "Description" };
                var optionalFields = new[] { "Active", "DoCopy", "CopyJobToFolder", "DoSave", "DoExport", "ExportFile", "DeActivateOnCompletion" };

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new JobStatusesImportService();

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import job statuses (from clipboard):\n\n" +
                                      $"New statuses: {preview.NewRecordCount}\n" +
                                      $"Updated statuses: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nImported: {result.ImportedCount} job statuses\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} statuses\n";
                    if (result.ErrorCount > 0)
                        successMsg += $"Errors: {result.ErrorCount} statuses\n";
                    successMsg += "\nSave database to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                        result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    dgJobStatuses_Loaded(dgJobStatuses, null);
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        /// <summary>
        /// Paste Button Report import from clipboard.
        /// </summary>
        private void btnPasteImportButtonReport_Click(object sender, RoutedEventArgs e)
        {
            PasteImportServiceTemplateData(matchByTemplate: false);
        }

        /// <summary>
        /// Paste Template Button Report import from clipboard.
        /// </summary>
        private void btnPasteImportTemplateButtonReport_Click(object sender, RoutedEventArgs e)
        {
            PasteImportServiceTemplateData(matchByTemplate: true);
        }

        /// <summary>
        /// Common handler for pasting service template data from clipboard.
        /// </summary>
        private void PasteImportServiceTemplateData(bool matchByTemplate)
        {
            ClipboardImportData clipData = null;
            try
            {
                clipData = GetClipboardAsImportFile();
                if (clipData == null) return;

                string[] requiredFields;
                string[] optionalFields;

                if (matchByTemplate)
                {
                    requiredFields = new[] { "Template Name", "Tab", "Name" };
                    optionalFields = new[] { "Button Code", "Item Path1", "Condition1", "Item Path2", "Condition2", "Item Path3", "Condition3", "Item Path4", "Condition4" };
                }
                else
                {
                    requiredFields = new[] { "Service Name", "Tab", "Name" };
                    optionalFields = new[] { "Template Name", "Button Code", "Item Path1", "Condition1", "Item Path2", "Condition2", "Item Path3", "Condition3", "Item Path4", "Condition4" };
                }

                var mappingWindow = new ColumnMappingWindow(
                    clipData.TempFilePath, requiredFields, optionalFields, clipData.Delimiter);
                mappingWindow.ShowDialog();

                if (!mappingWindow.DialogResultOk) return;

                var options = new ImportOptions
                {
                    HasHeaderRow = mappingWindow.HasHeaders,
                    UpdateExisting = true,
                    StopOnFirstError = false,
                    Delimiter = clipData.Delimiter
                };

                if (mappingWindow.ResultMapping != null)
                    options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                var importService = new ServiceTemplateDataImportService
                {
                    MatchByTemplate = matchByTemplate
                };

                var validation = importService.Validate(clipData.TempFilePath, options);
                if (!validation.IsValid)
                {
                    string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                    foreach (var error in validation.Errors.Take(5))
                        errorMsg += $"  {error}\n";
                    if (validation.Errors.Count > 5)
                        errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";
                    MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (validation.Warnings.Count > 0)
                {
                    string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                    foreach (var warning in validation.Warnings.Take(5))
                        warnMsg += $"  {warning}\n";
                    if (validation.Warnings.Count > 5)
                        warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                    warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";
                    if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                var preview = importService.Preview(clipData.TempFilePath, options);
                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import button report (from clipboard):\n\n" +
                                      $"Updated buttons: {preview.UpdatedRecordCount}\n" +
                                      $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\nContinue?";
                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = importService.Import(clipData.TempFilePath, options);

                if (result.IsSuccess)
                {
                    string successMsg = $"Import complete (from clipboard).\n\nUpdated: {result.ImportedCount} buttons\n";
                    if (result.SkippedCount > 0)
                        successMsg += $"Skipped: {result.SkippedCount} entries\n";
                    successMsg += "\nSave services/templates to persist changes.";
                    MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (result.WasCancelled)
                {
                    MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import failed: {result.ErrorMessage}",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error during paste import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (clipData != null) CleanupTempFile(clipData.TempFilePath);
            }
        }

        #endregion
    }
}
