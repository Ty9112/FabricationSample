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
        /// Prompts user to select price tables and exports them to a folder.
        /// </summary>
        private void btnExportPriceTables_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show price table selection dialog
                var selectionWindow = new PriceTableSelectionWindow();
                selectionWindow.ShowDialog();

                if (!selectionWindow.DialogResultOk)
                {
                    return; // User cancelled
                }

                var selectedTables = selectionWindow.SelectedPriceTables;

                if (selectedTables == null || selectedTables.Count == 0)
                {
                    MessageBox.Show("No price tables selected.", "Export Cancelled",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                        // Initialize service with selected tables
                        _priceExportService = new PriceTablesExportService();
                        _priceExportService.SelectedTables = selectedTables;
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
        /// Prompts user to select installation tables and exports them to a folder.
        /// </summary>
        private void btnExportInstallationTimes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show installation table selection dialog
                var selectionWindow = new InstallTableSelectionWindow();
                selectionWindow.ShowDialog();

                if (!selectionWindow.DialogResultOk)
                {
                    return; // User cancelled
                }

                var selectedTables = selectionWindow.SelectedInstallTables;

                if (selectedTables == null || selectedTables.Count == 0)
                {
                    MessageBox.Show("No installation tables selected.", "Export Cancelled",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                        // Initialize service with selected tables
                        _installExportService = new InstallationTimesExportService();
                        _installExportService.SelectedTables = selectedTables;
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

        /// <summary>
        /// Export button report - service template data with button codes.
        /// </summary>
        private void btnExportButtonReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pre-select the service currently chosen in cmbSelectService
                var preSelectedNames = new List<string>();
                var selectedService = cmbSelectService.SelectedItem as Service;
                if (selectedService != null)
                {
                    preSelectedNames.Add(selectedService.Name);
                }

                // Show service selection dialog with pre-selection
                var selectionWindow = new ServiceSelectionWindow(
                    preSelectedNames.Count > 0 ? preSelectedNames : null);
                selectionWindow.ShowDialog();

                if (!selectionWindow.DialogResultOk)
                {
                    return; // User cancelled
                }

                var selectedServices = selectionWindow.SelectedServiceNames;

                if (selectedServices == null || selectedServices.Count == 0)
                {
                    MessageBox.Show("No services selected.", "Export Cancelled",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Prompt for file location
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Button Report";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    // Create filename based on service selection
                    string filePrefix;
                    if (selectedServices.Count == 1)
                    {
                        // Single service: extract text from brackets [...]
                        string serviceName = selectedServices[0];
                        string bracketContent = ExtractBracketContent(serviceName);

                        if (!string.IsNullOrEmpty(bracketContent))
                        {
                            filePrefix = bracketContent.Replace(" ", "_");
                        }
                        else
                        {
                            // No brackets found, use full name
                            filePrefix = serviceName.Replace(" ", "_");
                        }
                    }
                    else
                    {
                        // Multiple services: use "MultipleServices"
                        filePrefix = "MultipleServices";
                    }

                    saveDialog.FileName = $"{filePrefix}_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFile = saveDialog.FileName;

                        if (string.IsNullOrEmpty(outputFile))
                        {
                            MessageBox.Show("No file selected.", "Export Cancelled",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Initialize service
                        var exportService = new ServiceTemplateDataExportService
                        {
                            SelectedServiceNames = selectedServices
                        };
                        exportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        var exportWorker = new BackgroundWorker();
                        exportWorker.WorkerReportsProgress = true;
                        exportWorker.DoWork += (s, args) =>
                        {
                            var options = new ExportOptions { IncludeHeader = true };
                            var result = exportService.Export(outputFile, options);
                            args.Result = result;
                        };
                        exportWorker.ProgressChanged += (s, args) =>
                        {
                            // Could add a progress indicator here if desired
                        };
                        exportWorker.RunWorkerCompleted += (s, args) =>
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
                                        var response = MessageBox.Show(
                                            $"Button report exported successfully!\n\n" +
                                            $"File: {outputFile}\n" +
                                            $"Rows: {result.RowCount}\n" +
                                            $"Services: {selectedServices.Count}\n\n" +
                                            $"Open file location?",
                                            "Export Complete",
                                            MessageBoxButton.YesNo, MessageBoxImage.Information);

                                        if (response == MessageBoxResult.Yes)
                                        {
                                            System.Diagnostics.Process.Start("explorer.exe",
                                                $"/select,\"{outputFile}\"");
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting button report export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export button report by template - service template data grouped by template.
        /// </summary>
        private void btnExportTemplateButtonReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pre-select the template currently chosen in cmbSelectServiceTemplate
                var preSelectedNames = new List<string>();
                var selectedTemplate = cmbSelectServiceTemplate.SelectedItem as ServiceTemplate;
                if (selectedTemplate != null)
                {
                    preSelectedNames.Add(selectedTemplate.Name);
                }

                // Show service template selection dialog with pre-selection
                var selectionWindow = new ServiceTemplateSelectionWindow(
                    preSelectedNames.Count > 0 ? preSelectedNames : null);
                selectionWindow.ShowDialog();

                if (!selectionWindow.DialogResultOk)
                {
                    return; // User cancelled
                }

                var selectedTemplates = selectionWindow.SelectedTemplateNames;

                if (selectedTemplates == null || selectedTemplates.Count == 0)
                {
                    MessageBox.Show("No service templates selected.", "Export Cancelled",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Prompt for file location
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Button Report by Template";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    // Create filename based on template selection
                    string filePrefix;
                    if (selectedTemplates.Count == 1)
                    {
                        // Single template: extract text from brackets [...]
                        string templateName = selectedTemplates[0];
                        string bracketContent = ExtractBracketContent(templateName);

                        if (!string.IsNullOrEmpty(bracketContent))
                        {
                            filePrefix = bracketContent.Replace(" ", "_");
                        }
                        else
                        {
                            // No brackets found, use full name
                            filePrefix = templateName.Replace(" ", "_");
                        }
                    }
                    else
                    {
                        // Multiple templates: use "MultipleTemplates"
                        filePrefix = "MultipleTemplates";
                    }

                    saveDialog.FileName = $"{filePrefix}_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        string outputFile = saveDialog.FileName;

                        if (string.IsNullOrEmpty(outputFile))
                        {
                            MessageBox.Show("No file selected.", "Export Cancelled",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Initialize service with template export mode
                        var exportService = new ServiceTemplateDataExportService
                        {
                            ExportByTemplate = true,
                            SelectedTemplateNames = selectedTemplates
                        };
                        exportService.ProgressChanged += ExportService_ProgressChanged;

                        // Setup background worker
                        var exportWorker = new BackgroundWorker();
                        exportWorker.WorkerReportsProgress = true;
                        exportWorker.DoWork += (s, args) =>
                        {
                            var options = new ExportOptions { IncludeHeader = true };
                            var result = exportService.Export(outputFile, options);
                            args.Result = result;
                        };
                        exportWorker.ProgressChanged += (s, args) =>
                        {
                            // Could add a progress indicator here if desired
                        };
                        exportWorker.RunWorkerCompleted += (s, args) =>
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
                                        var response = MessageBox.Show(
                                            $"Button report exported successfully!\n\n" +
                                            $"File: {outputFile}\n" +
                                            $"Rows: {result.RowCount}\n" +
                                            $"Templates: {selectedTemplates.Count}\n\n" +
                                            $"Open file location?",
                                            "Export Complete",
                                            MessageBoxButton.YesNo, MessageBoxImage.Information);

                                        if (response == MessageBoxResult.Yes)
                                        {
                                            System.Diagnostics.Process.Start("explorer.exe",
                                                $"/select,\"{outputFile}\"");
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Failed",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                    }
                                }
                            });
                        };

                        exportWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting template button report export: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Extract text from within square brackets [].
        /// Returns the content inside the first pair of brackets found.
        /// </summary>
        private string ExtractBracketContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int startIndex = text.IndexOf('[');
            int endIndex = text.IndexOf(']');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                return text.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Export Item Statuses to CSV.
        /// </summary>
        private void btnExportItemStatuses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Item Statuses";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"ItemStatuses_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var exportService = new ItemStatusesExportService();
                        var options = new ExportOptions { IncludeHeader = true };
                        var result = exportService.Export(saveDialog.FileName, options);

                        if (result.IsSuccess)
                        {
                            var response = MessageBox.Show(
                                $"Item statuses exported successfully!\n\n" +
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
                MessageBox.Show($"Error exporting item statuses: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Export Job Statuses to CSV.
        /// </summary>
        private void btnExportJobStatuses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "Export Job Statuses";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    saveDialog.FileName = $"JobStatuses_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var exportService = new JobStatusesExportService();
                        var options = new ExportOptions { IncludeHeader = true };
                        var result = exportService.Export(saveDialog.FileName, options);

                        if (result.IsSuccess)
                        {
                            var response = MessageBox.Show(
                                $"Job statuses exported successfully!\n\n" +
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
                MessageBox.Show($"Error exporting job statuses: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
