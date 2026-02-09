using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.Data;
using FabricationSample.Services.Export;
using FabricationSample.Services.Import;
using MessageBox = System.Windows.MessageBox;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Specifications functionality
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        #region Specifications Tab

        private ObservableCollection<SpecificationGridItem> _specifications;

        /// <summary>
        /// Load Specifications tab when selected.
        /// </summary>
        private void tbiSpecifications_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecifications();
        }

        /// <summary>
        /// Load all specifications into the grid.
        /// </summary>
        private void LoadSpecifications()
        {
            _specifications = new ObservableCollection<SpecificationGridItem>();

            try
            {
                var specs = Database.Specifications;
                if (specs != null)
                {
                    foreach (Specification spec in specs.OrderBy(s => s.Group).ThenBy(s => s.Name))
                    {
                        _specifications.Add(new SpecificationGridItem(spec));
                    }
                }

                dgSpecifications.ItemsSource = _specifications;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading specifications: {ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Specifications to CSV.
        /// </summary>
        private void btnExportSpecifications_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Specifications";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"Specifications_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var exportService = new SpecificationsExportService();
                        exportService.ProgressChanged += (s, args) =>
                        {
                            Dispatcher.Invoke(() => { prgSpecifications.Value = args.Percentage; });
                        };

                        var options = new ExportOptions { IncludeHeader = true };
                        var result = exportService.Export(saveDialog.FileName, options);

                        prgSpecifications.Value = 0;

                        if (result.IsSuccess)
                        {
                            var response = MessageBox.Show(
                                $"Specifications exported successfully!\n\n" +
                                $"File: {saveDialog.FileName}\n" +
                                $"Rows: {result.RowCount}\n\n" +
                                $"Open file location?",
                                "Export Complete",
                                MessageBoxButton.YesNo, MessageBoxImage.Information);

                            if (response == MessageBoxResult.Yes)
                            {
                                System.Diagnostics.Process.Start("explorer.exe",
                                    $"/select,\"{saveDialog.FileName}\"");
                            }
                        }
                        else
                        {
                            MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting specifications: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Import Specifications from CSV.
        /// </summary>
        private void btnImportSpecifications_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new OpenFileDialog())
                {
                    fileDialog.Title = "Select Specifications CSV file to import";
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
                    var optionalFields = new[] { "Group", "Description" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    var options = new ImportOptions
                    {
                        HasHeaderRow = true,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    var importService = new SpecificationsImportService();

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
                        string previewMsg = $"Ready to import specifications:\n\n" +
                                          $"New specifications: {preview.NewRecordCount}\n" +
                                          $"Updated specifications: {preview.UpdatedRecordCount}\n" +
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
                    importService.ProgressChanged += (s, args) =>
                    {
                        Dispatcher.Invoke(() => { prgSpecifications.Value = args.Percentage; });
                    };

                    var result = importService.Import(importFile, options);

                    prgSpecifications.Value = 0;

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Imported: {result.ImportedCount} specifications\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} specifications\n";
                        if (result.ErrorCount > 0)
                            successMsg += $"Errors: {result.ErrorCount} specifications\n";
                        successMsg += "\nClick 'Save Specifications' to persist changes.";

                        MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                        // Reload the specifications grid
                        LoadSpecifications();
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
        /// Save Specifications changes.
        /// </summary>
        private void btnSaveSpecifications_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = Database.SaveSpecifications();
                if (result.Status == ResultStatus.Succeeded)
                {
                    MessageBox.Show("Specifications saved successfully.", "Save Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to save specifications.", "Save Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving specifications: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
