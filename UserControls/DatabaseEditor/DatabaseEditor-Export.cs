using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.Fabrication.DB;
using FabricationSample.Services.Export;
using FabricationSample.Utilities;
using MessageBox = System.Windows.MessageBox;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Export functionality
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        #region Export Services

        private PriceTablesExportService _priceExportService;
        private InstallationTimesExportService _installExportService;
        private ItemDataExportService _itemDataExportService;
        private ProductInfoExportService _productInfoExportService;
        private BackgroundWorker _exportWorker;

        #endregion

        #region Export Button Handlers

        /// <summary>
        /// Export Price Tables button click handler.
        /// Prompts user for output folder and exports all price lists and breakpoint tables.
        /// </summary>
        private void btnExportPriceTables_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prompt for folder
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select folder to export price tables";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFolder = folderDialog.SelectedPath;

                        if (string.IsNullOrEmpty(outputFolder))
                        {
                            MessageBox.Show("No folder selected.", "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Create timestamped subfolder
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string exportFolder = Path.Combine(outputFolder, $"PriceTables_{timestamp}");

                        // Initialize service
                        _priceExportService = new PriceTablesExportService();
                        _priceExportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        _exportWorker = new BackgroundWorker();
                        _exportWorker.WorkerReportsProgress = true;
                        _exportWorker.DoWork += (s, args) =>
                        {
                            var result = _priceExportService.Export(exportFolder);
                            args.Result = result;
                        };
                        _exportWorker.ProgressChanged += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                prgPriceList.Value = args.ProgressPercentage;
                            });
                        };
                        _exportWorker.RunWorkerCompleted += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                prgPriceList.Value = 0;

                                if (args.Error != null)
                                {
                                    MessageBox.Show($"Export failed: {args.Error.Message}", "Export Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else if (args.Result is PriceTablesExportResult result)
                                {
                                    if (result.IsSuccess)
                                    {
                                        string message = $"Export completed successfully!\n\n" +
                                                       $"Folder: {result.FolderPath}\n" +
                                                       $"Files Created: {result.FileCount}\n" +
                                                       $"Simple Price Lists: {result.SimpleListCount}\n" +
                                                       $"Breakpoint Tables: {result.BreakpointTableCount}";

                                        MessageBox.Show(message, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else if (result.WasCancelled)
                                    {
                                        MessageBox.Show("Export was cancelled.", "Export Cancelled",
                                            MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        _exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Installation Times button click handler.
        /// Prompts user for output folder and exports all installation times tables.
        /// </summary>
        private void btnExportInstallationTimes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prompt for folder
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select folder to export installation times";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFolder = folderDialog.SelectedPath;

                        if (string.IsNullOrEmpty(outputFolder))
                        {
                            MessageBox.Show("No folder selected.", "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Create timestamped subfolder
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string exportFolder = Path.Combine(outputFolder, $"InstallationTimes_{timestamp}");

                        // Initialize service
                        _installExportService = new InstallationTimesExportService();
                        _installExportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        _exportWorker = new BackgroundWorker();
                        _exportWorker.WorkerReportsProgress = true;
                        _exportWorker.DoWork += (s, args) =>
                        {
                            var result = _installExportService.Export(exportFolder);
                            args.Result = result;
                        };
                        _exportWorker.ProgressChanged += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                prgInstallationTimes.Value = args.ProgressPercentage;
                            });
                        };
                        _exportWorker.RunWorkerCompleted += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                prgInstallationTimes.Value = 0;

                                if (args.Error != null)
                                {
                                    MessageBox.Show($"Export failed: {args.Error.Message}", "Export Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else if (args.Result is InstallationTimesExportResult result)
                                {
                                    if (result.IsSuccess)
                                    {
                                        string message = $"Export completed successfully!\n\n" +
                                                       $"Folder: {result.FolderPath}\n" +
                                                       $"Files Created: {result.FileCount}\n" +
                                                       $"Simple Tables: {result.SimpleTableCount}\n" +
                                                       $"Breakpoint Tables: {result.BreakpointTableCount}\n" +
                                                       $"Product Entries: {result.ProductEntryCount}";

                                        MessageBox.Show(message, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else if (result.WasCancelled)
                                    {
                                        MessageBox.Show("Export was cancelled.", "Export Cancelled",
                                            MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        _exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Item Data button click handler.
        /// Prompts user for output file and exports all service item data with product lists.
        /// </summary>
        private void btnExportItemData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prompt for file location
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Item Data";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"ItemData_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFile = saveDialog.FileName;

                        if (string.IsNullOrEmpty(outputFile))
                        {
                            MessageBox.Show("No file selected.", "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Initialize service
                        _itemDataExportService = new ItemDataExportService();
                        _itemDataExportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        _exportWorker = new BackgroundWorker();
                        _exportWorker.WorkerReportsProgress = true;
                        _exportWorker.DoWork += (s, args) =>
                        {
                            var result = _itemDataExportService.Export(outputFile);
                            args.Result = result;
                        };
                        _exportWorker.ProgressChanged += (s, args) =>
                        {
                            // Could add a progress indicator here if desired
                        };
                        _exportWorker.RunWorkerCompleted += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                if (args.Error != null)
                                {
                                    MessageBox.Show($"Export failed: {args.Error.Message}", "Export Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else if (args.Result is ExportResult result)
                                {
                                    if (result.IsSuccess)
                                    {
                                        string message = $"Export completed successfully!\n\n" +
                                                       $"File: {result.FilePath}\n" +
                                                       $"Records Exported: {result.RecordCount}";

                                        MessageBox.Show(message, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else if (result.WasCancelled)
                                    {
                                        MessageBox.Show("Export was cancelled.", "Export Cancelled",
                                            MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        _exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle progress updates from export services.
        /// Updates the appropriate progress bar based on which export is running.
        /// </summary>
        private void ExportService_ProgressChanged(object sender, ProgressEventArgs e)
        {
            if (_exportWorker != null && _exportWorker.IsBusy)
            {
                _exportWorker.ReportProgress(e.Percentage);
            }
        }

        /// <summary>
        /// Export Product Database grid to CSV.
        /// Exports the currently displayed product definitions with all visible columns.
        /// </summary>
        private void btnExportProductGrid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = dgMapprod.ItemsSource;
                if (items == null)
                {
                    MessageBox.Show("No product data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Product Database Grid";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"ProductDatabase_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var csvLines = new List<string>();

                        // Header row with all columns including supplier IDs
                        var headerParts = new List<string>
                        {
                            "Id", "Group", "InstallType", "Description", "Finish",
                            "Specification", "Material", "ProductName", "Range", "Size",
                            "Manufacturer", "Source"
                        };

                        // Get unique supplier names for dynamic columns
                        var supplierNames = new List<string>();
                        foreach (ProductDefinition def in items)
                        {
                            if (def.SupplierIds != null)
                            {
                                foreach (var supplierId in def.SupplierIds)
                                {
                                    string supplierName = supplierId.ProductSupplier?.Name ?? "Unknown";
                                    if (!supplierNames.Contains(supplierName))
                                        supplierNames.Add(supplierName);
                                }
                            }
                        }
                        headerParts.AddRange(supplierNames);
                        csvLines.Add(CsvHelpers.WrapForCsv((object[])headerParts.ToArray()));

                        // Data rows
                        int count = 0;
                        foreach (ProductDefinition def in items)
                        {
                            var rowParts = new List<string>
                            {
                                def.Id ?? "",
                                def.Group?.Name ?? "",
                                def.InstallType ?? "",
                                def.Description ?? "",
                                def.Finish ?? "",
                                def.Specification ?? "",
                                def.Material ?? "",
                                def.ProductName ?? "",
                                def.Range ?? "",
                                def.Size ?? "",
                                def.Manufacturer ?? "",
                                def.Source ?? ""
                            };

                            // Add supplier ID values for each supplier column
                            foreach (var supplierName in supplierNames)
                            {
                                string supplierIdValue = "";
                                if (def.SupplierIds != null)
                                {
                                    var match = def.SupplierIds.FirstOrDefault(s =>
                                        (s.ProductSupplier?.Name ?? "Unknown") == supplierName);
                                    if (match != null)
                                        supplierIdValue = match.Id ?? "";
                                }
                                rowParts.Add(supplierIdValue);
                            }

                            csvLines.Add(CsvHelpers.WrapForCsv((object[])rowParts.ToArray()));
                            count++;
                        }

                        File.WriteAllLines(saveDialog.FileName, csvLines, Encoding.UTF8);

                        MessageBox.Show($"Export completed successfully!\n\nFile: {saveDialog.FileName}\nRecords: {count}",
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting grid: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export full product info using ProductInfoExportService.
        /// This is equivalent to the GetProductInfo NETLOAD command from DiscordCADmep.
        /// Includes product definitions, supplier IDs, price lists, installation times, and breakpoint labor.
        /// </summary>
        private void btnGetProductInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Full Product Info (GetProductInfo)";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"ProductInfo_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFile = saveDialog.FileName;

                        if (string.IsNullOrEmpty(outputFile))
                        {
                            MessageBox.Show("No file selected.", "Export Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Initialize service
                        _productInfoExportService = new ProductInfoExportService();
                        _productInfoExportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        _exportWorker = new BackgroundWorker();
                        _exportWorker.WorkerReportsProgress = true;
                        _exportWorker.DoWork += (s, args) =>
                        {
                            var result = _productInfoExportService.Export(outputFile);
                            args.Result = result;
                        };
                        _exportWorker.ProgressChanged += (s, args) =>
                        {
                            // Progress updates handled by service
                        };
                        _exportWorker.RunWorkerCompleted += (s, args) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                if (args.Error != null)
                                {
                                    MessageBox.Show($"Export failed: {args.Error.Message}", "Export Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else if (args.Result is ExportResult result)
                                {
                                    if (result.IsSuccess)
                                    {
                                        string message = $"Full Product Info export completed!\n\n" +
                                                       $"File: {result.FilePath}\n" +
                                                       $"Records Exported: {result.RecordCount}\n\n" +
                                                       "Includes: Product definitions, Supplier IDs, Price Lists,\n" +
                                                       "Installation Times, and Breakpoint Labor values.";

                                        MessageBox.Show(message, "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else if (result.WasCancelled)
                                    {
                                        MessageBox.Show("Export was cancelled.", "Export Cancelled",
                                            MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        _exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Supplier Discounts grid to CSV.
        /// Exports all discount codes, values, and descriptions for all supplier groups.
        /// </summary>
        private void btnExportSupplierDiscounts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Supplier Discounts";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"SupplierDiscounts_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var csvLines = new List<string>();

                        // Header row
                        csvLines.Add(CsvHelpers.WrapForCsv("SupplierGroup", "Code", "Value", "Description"));

                        // Data rows - export all discounts from all supplier groups
                        int count = 0;
                        foreach (var supplierGroup in Database.SupplierGroups)
                        {
                            string groupName = supplierGroup.Name ?? "";
                            if (supplierGroup.Discounts?.Discounts != null)
                            {
                                foreach (var discount in supplierGroup.Discounts.Discounts)
                                {
                                    csvLines.Add(CsvHelpers.WrapForCsv(
                                        groupName,
                                        discount.Code ?? "",
                                        discount.Value.ToString(),
                                        discount.Description ?? ""
                                    ));
                                    count++;
                                }
                            }
                        }

                        File.WriteAllLines(saveDialog.FileName, csvLines, Encoding.UTF8);

                        MessageBox.Show($"Export completed successfully!\n\nFile: {saveDialog.FileName}\nDiscounts: {count}",
                            "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting discounts: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
