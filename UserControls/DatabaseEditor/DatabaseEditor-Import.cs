using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Autodesk.Fabrication.DB;
using FabricationSample.Services.Import;
using MessageBox = System.Windows.MessageBox;

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

                    // Create import service
                    var importService = new PriceTableImportService(priceList);

                    // Validate file
                    var validation = importService.Validate(importFile);

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

                    // Generate preview
                    var preview = importService.Preview(importFile);

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

                    // Perform import
                    var options = new ImportOptions
                    {
                        HasHeaderRow = true,
                        UpdateExisting = true, // Allow updating existing entries
                        StopOnFirstError = false
                    };

                    importService.ProgressChanged += (s, args) =>
                    {
                        // Update progress on UI thread
                        Dispatcher.Invoke(() =>
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

        #endregion
    }
}
