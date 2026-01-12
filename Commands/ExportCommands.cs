using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using FabricationSample.Services.Export;
using FabricationSample.Utilities;
using CADapp = Autodesk.AutoCAD.ApplicationServices.Application;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Commands
{
    /// <summary>
    /// NETLOAD export commands for Fabrication data.
    /// Ported from DiscordCADmep with service layer architecture.
    /// </summary>
    public class ExportCommands
    {
        #region Helper Methods

        /// <summary>
        /// Validate that Fabrication API is loaded and accessible.
        /// </summary>
        /// <returns>True if API is accessible, false otherwise</returns>
        private static bool ValidateFabricationLoaded()
        {
            try
            {
                // Try to access fabrication database
                var services = FabDB.Services;
                return services != null;
            }
            catch
            {
                MessageBox.Show(
                    "Fabrication API is not loaded.\n\nPlease load CADmep and open a valid fabrication job.",
                    "Fabrication API Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Prompt user for export folder location.
        /// </summary>
        /// <param name="exportType">Description of export type for dialog title</param>
        /// <returns>Selected folder path, or null if user cancelled</returns>
        private static string PromptForExportLocation(string exportType)
        {
            try
            {
                string defaultFolder = FileHelpers.GetDefaultExportFolder();
                string title = $"Select output folder for {exportType}";

                return FileHelpers.PromptForExportFolder(title, defaultFolder);
            }
            catch (System.Exception ex)
            {
                ShowError($"Error selecting folder: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate timestamped file path for export.
        /// </summary>
        /// <param name="folder">Output folder</param>
        /// <param name="baseName">Base name for file</param>
        /// <returns>Full file path with timestamp</returns>
        private static string GenerateTimestampedPath(string folder, string baseName)
        {
            return FileHelpers.GenerateTimestampedFilePath(folder, baseName);
        }

        /// <summary>
        /// Show error message to user.
        /// </summary>
        /// <param name="message">Error message</param>
        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Princ($"ERROR: {message}");
        }

        /// <summary>
        /// Show success message and optionally open file.
        /// </summary>
        /// <param name="filePath">Path to exported file</param>
        /// <param name="rowCount">Number of rows exported</param>
        private static void ShowSuccess(string filePath, int rowCount = 0)
        {
            string message = rowCount > 0
                ? $"Export complete: {filePath}\n\n{rowCount} rows exported.\n\nOpen file?"
                : $"Export complete: {filePath}\n\nOpen file?";

            if (MessageBox.Show(message, "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                FileHelpers.OpenFileInExplorer(filePath);
            }
        }

        /// <summary>
        /// Show success message for folder exports and optionally open folder.
        /// </summary>
        /// <param name="folderPath">Path to export folder</param>
        /// <param name="fileCount">Number of files exported</param>
        /// <param name="rowCount">Total number of rows exported</param>
        private static void ShowFolderSuccess(string folderPath, int fileCount, int rowCount = 0)
        {
            string message = rowCount > 0
                ? $"Export complete.\n\nFiles: {fileCount}\nTotal rows: {rowCount}\nLocation: {folderPath}\n\nOpen folder?"
                : $"Export complete.\n\nFiles: {fileCount}\nLocation: {folderPath}\n\nOpen folder?";

            if (MessageBox.Show(message, "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                FileHelpers.OpenFolderInExplorer(folderPath);
            }
        }

        /// <summary>
        /// Write message to AutoCAD command line.
        /// </summary>
        /// <param name="message">Message to write</param>
        private static void Princ(string message)
        {
            try
            {
                var ed = CADapp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n{message}");
            }
            catch
            {
                // Silently fail if command line not available
            }
        }

        /// <summary>
        /// Log error to file for debugging.
        /// </summary>
        /// <param name="commandName">Name of command that failed</param>
        /// <param name="ex">Exception that occurred</param>
        private static void LogError(string commandName, System.Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FabricationSample",
                    "errors.log"
                );

                FileHelpers.EnsureDirectoryExists(Path.GetDirectoryName(logPath));

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {commandName}: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        #endregion

        #region Export Commands

        /// <summary>
        /// Export comprehensive product information including definitions, prices, and labor.
        /// This is the most comprehensive export, combining all product-related data.
        /// </summary>
        [CommandMethod("GetProductInfo")]
        public static void GetProductInfo()
        {
            try
            {
                // 1. Validate environment
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting product info export...");

                // 2. Get export location
                string exportFolder = PromptForExportLocation("Product Info");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return; // User cancelled
                }

                // 3. Generate output path
                string exportPath = GenerateTimestampedPath(exportFolder, "ProductInfo");

                // 4. Create service and execute export
                Princ("Generating product info CSV...");
                var exportService = new ProductInfoExportService();

                // Subscribe to progress events
                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions
                {
                    IncludeHeader = true,
                    OpenAfterExport = true
                };

                var result = exportService.Export(exportPath, options);

                // 5. Handle result
                if (result.IsSuccess)
                {
                    Princ($"Export complete: {result.RowCount} rows exported to {exportPath}");
                    ShowSuccess(result.FilePath, result.RowCount);
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("GetProductInfo", ex);
            }
        }

        /// <summary>
        /// Export service items with product list entries and template conditions.
        /// Useful for reviewing item assignments and conditions across all services.
        /// </summary>
        [CommandMethod("ExportItemData")]
        public static void ExportItemData()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting item data export...");

                string exportFolder = PromptForExportLocation("Item Data");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return;
                }

                string exportPath = GenerateTimestampedPath(exportFolder, "ItemReport");

                Princ("Generating item data CSV...");
                var exportService = new ItemDataExportService();

                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions
                {
                    IncludeHeader = true,
                    OpenAfterExport = true
                };

                var result = exportService.Export(exportPath, options);

                if (result.IsSuccess)
                {
                    Princ($"Export complete: {result.RowCount} rows exported to {exportPath}");
                    ShowSuccess(result.FilePath, result.RowCount);
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("ExportItemData", ex);
            }
        }

        /// <summary>
        /// Export price tables including both simple price lists and breakpoint tables.
        /// Creates a folder with multiple CSV files: main price list + breakpoint tables.
        /// </summary>
        [CommandMethod("GetPriceTables")]
        public static void GetPriceTables()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting price tables export...");

                string exportFolder = PromptForExportLocation("Price Tables");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return;
                }

                // Create timestamped subfolder
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputFolder = Path.Combine(exportFolder, $"PriceTables_{timestamp}");

                Princ("Generating price tables...");
                var exportService = new PriceTablesExportService();

                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions { IncludeHeader = true };
                var result = exportService.Export(outputFolder, options);

                if (result.IsSuccess)
                {
                    Princ($"Export complete: {result.FileCount} files, {result.SimpleListCount} price entries, {result.BreakpointTableCount} breakpoint tables");
                    ShowFolderSuccess(result.FolderPath, result.FileCount, result.SimpleListCount);
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("GetPriceTables", ex);
            }
        }

        /// <summary>
        /// Export installation times tables including both simple and breakpoint tables.
        /// Creates a folder with multiple CSV files: main product list + breakpoint tables.
        /// </summary>
        [CommandMethod("GetInstallationTimes")]
        public static void GetInstallationTimes()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting installation times export...");

                string exportFolder = PromptForExportLocation("Installation Times");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return;
                }

                // Create timestamped subfolder
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outputFolder = Path.Combine(exportFolder, $"InstallationTimes_{timestamp}");

                Princ("Generating installation times...");
                var exportService = new InstallationTimesExportService();

                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions { IncludeHeader = true };
                var result = exportService.Export(outputFolder, options);

                if (result.IsSuccess)
                {
                    string message = $"Installation Times exported.\n\n" +
                        $"Simple tables: {result.SimpleTableCount}\n" +
                        $"Breakpoint tables: {result.BreakpointTableCount}\n" +
                        $"Other: {result.OtherTableCount}\n" +
                        $"Product entries: {result.ProductEntryCount}\n\n" +
                        $"Open folder?";

                    Princ($"Export complete: {result.FileCount} files, {result.SimpleTableCount} simple tables, {result.BreakpointTableCount} breakpoint tables");

                    if (MessageBox.Show(message, "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        FileHelpers.OpenFolderInExplorer(result.FolderPath);
                    }
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("GetInstallationTimes", ex);
            }
        }

        /// <summary>
        /// Export items with calculated labor values from breakpoint tables.
        /// Shows dimensions, installation table assignments, and calculated labor for each product.
        /// </summary>
        [CommandMethod("GetItemLabor")]
        public static void GetItemLabor()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting item labor export...");

                string exportFolder = PromptForExportLocation("Item Labor");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return;
                }

                string exportPath = GenerateTimestampedPath(exportFolder, "ItemLabor");

                Princ("Generating item labor CSV...");
                var exportService = new ItemLaborExportService();

                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions
                {
                    IncludeHeader = true,
                    OpenAfterExport = true
                };

                var result = exportService.Export(exportPath, options);

                if (result.IsSuccess)
                {
                    Princ($"Export complete: {result.RowCount} rows exported to {exportPath}");
                    ShowSuccess(result.FilePath, result.RowCount);
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("GetItemLabor", ex);
            }
        }

        /// <summary>
        /// Export items showing their assigned installation times tables.
        /// Useful for reviewing which installation table each item is configured to use.
        /// </summary>
        [CommandMethod("GetItemInstallationTables")]
        public static void GetItemInstallationTables()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Starting item installation tables export...");

                string exportFolder = PromptForExportLocation("Item Installation Tables");
                if (string.IsNullOrEmpty(exportFolder))
                {
                    Princ("Export cancelled: No folder selected.");
                    return;
                }

                string exportPath = GenerateTimestampedPath(exportFolder, "ItemInstallationTables");

                Princ("Generating item installation tables CSV...");
                var exportService = new ItemInstallationTablesExportService();

                exportService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ExportOptions
                {
                    IncludeHeader = true,
                    OpenAfterExport = true
                };

                var result = exportService.Export(exportPath, options);

                if (result.IsSuccess)
                {
                    Princ($"Export complete: {result.RowCount} rows exported to {exportPath}");
                    ShowSuccess(result.FilePath, result.RowCount);
                }
                else if (result.WasCancelled)
                {
                    Princ("Export was cancelled by user.");
                    MessageBox.Show("Export was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Export failed: {result.ErrorMessage}");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("GetItemInstallationTables", ex);
            }
        }

        #endregion
    }
}
