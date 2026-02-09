using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Fabrication.Content;
using FabricationSample.Manager;
using FabricationSample.ProfileCopy.Windows;
using FabricationSample.Services.Import;
using FabricationSample.Utilities;
using CADapp = Autodesk.AutoCAD.ApplicationServices.Application;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Commands
{
    /// <summary>
    /// NETLOAD import commands for Fabrication data.
    /// Provides quick-access commands to import CSV data back into the fabrication database.
    /// </summary>
    public class ImportCommands
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
        /// Prompt user for import file location.
        /// </summary>
        /// <param name="importType">Description of import type for dialog title</param>
        /// <returns>Selected file path, or null if user cancelled</returns>
        private static string PromptForImportFile(string importType)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = $"Select CSV file to import: {importType}";
                    dialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;

                    // Try to set default directory to recent export location
                    string defaultFolder = FileHelpers.GetDefaultExportFolder();
                    if (Directory.Exists(defaultFolder))
                    {
                        dialog.InitialDirectory = defaultFolder;
                    }

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return dialog.FileName;
                    }
                }

                return null;
            }
            catch (System.Exception ex)
            {
                ShowError($"Error selecting file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Show error message to user.
        /// </summary>
        /// <param name="message">Error message</param>
        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Princ($"ERROR: {message}");
        }

        /// <summary>
        /// Show success message with import summary.
        /// </summary>
        /// <param name="result">Import result</param>
        private static void ShowSuccess(ImportResult result)
        {
            string message = $"Import complete.\n\n" +
                           $"Imported: {result.ImportedCount} records\n";

            if (result.SkippedCount > 0)
                message += $"Skipped: {result.SkippedCount} records\n";

            if (result.ErrorCount > 0)
            {
                message += $"Errors: {result.ErrorCount} records\n\n";
                message += "Check the command line for error details.";

                // Print errors to command line
                foreach (var error in result.Errors.Take(10)) // Limit to first 10 errors
                {
                    Princ($"  Line {error.Key}: {error.Value}");
                }

                if (result.Errors.Count > 10)
                    Princ($"  ... and {result.Errors.Count - 10} more errors");
            }

            MessageBox.Show(message, "Import Complete", MessageBoxButtons.OK,
                result.ErrorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>
        /// Show validation result to user and ask if they want to continue.
        /// </summary>
        /// <param name="validation">Validation result</param>
        /// <returns>True if user wants to continue, false otherwise</returns>
        private static bool ShowValidationResult(Services.Import.ValidationResult validation)
        {
            if (validation.IsValid && validation.Warnings.Count == 0)
            {
                // No issues, continue silently
                return true;
            }

            string message = "";

            if (!validation.IsValid)
            {
                message = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                foreach (var error in validation.Errors.Take(5))
                {
                    message += $"  {error}\n";
                }
                if (validation.Errors.Count > 5)
                    message += $"  ... and {validation.Errors.Count - 5} more errors\n";

                message += "\nImport cannot continue.";

                MessageBox.Show(message, "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (validation.Warnings.Count > 0)
            {
                message = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                foreach (var warning in validation.Warnings.Take(5))
                {
                    message += $"  {warning}\n";
                }
                if (validation.Warnings.Count > 5)
                    message += $"  ... and {validation.Warnings.Count - 5} more warnings\n";

                message += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                return MessageBox.Show(message, "Validation Warnings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            }

            return true;
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

        #region Import Commands

        /// <summary>
        /// Import product list data from CSV into the current item.
        /// CSV must contain: Name, Weight, Id columns, plus optional DIM: and OPT: columns.
        /// NOTE: This command is temporarily disabled due to API type resolution issues.
        /// Use the ItemEditor UI for product list imports for now.
        /// </summary>
        [CommandMethod("ImportProductList")]
        public static void ImportProductList()
        {
            try
            {
                MessageBox.Show(
                    "Product List Import:\n\n" +
                    "This command is temporarily disabled due to API compatibility issues.\n\n" +
                    "Please use the existing CSV import functionality in the ItemEditor\n" +
                    "(Item Editor > Product List tab > Load button)\n\n" +
                    "This will be fixed in a future update.",
                    "Feature Not Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Princ("Product list import: Use ItemEditor UI for now.");
                return;

                // TODO: Re-enable when API type issues are resolved
                // The ProductListImportService requires proper type resolution for
                // Autodesk.Fabrication.Content.Item and related types

                /* COMMENTED OUT - TO BE FIXED
                // 1. Validate environment
                if (!ValidateFabricationLoaded())
                    return;

                // Check if there's a current item
                if (FabricationManager.CurrentItem == null)
                {
                    MessageBox.Show(
                        "No current item loaded.\n\nPlease load an item in the Item Editor before importing product list data.",
                        "No Item Loaded",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                Princ("Starting product list import...");

                // 2. Get import file
                string importFile = PromptForImportFile("Product List");
                if (string.IsNullOrEmpty(importFile))
                {
                    Princ("Import cancelled: No file selected.");
                    return;
                }

                // 3. Create import service
                var importService = new ProductListImportService(FabricationManager.CurrentItem);

                // 4. Validate file
                Princ("Validating CSV file...");
                var validation = importService.Validate(importFile);

                if (!ShowValidationResult(validation))
                {
                    Princ("Import cancelled: Validation failed.");
                    return;
                }

                // 5. Show preview (optional)
                Princ("Generating import preview...");
                var preview = importService.Preview(importFile);

                if (preview.IsSuccess)
                {
                    string previewMsg = $"Ready to import product list data:\n\n" +
                                      $"New rows: {preview.NewRecordCount}\n" +
                                      $"Total changes: {preview.Changes.Count}\n\n" +
                                      $"This will replace the existing product list.\n\n" +
                                      $"Continue?";

                    if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    {
                        Princ("Import cancelled by user.");
                        return;
                    }
                }

                // 6. Perform import
                Princ("Importing product list data...");

                importService.ProgressChanged += (sender, args) =>
                {
                    Princ($"  {args.Message}");
                };

                var options = new ImportOptions
                {
                    HasHeaderRow = true,
                    UpdateExisting = true
                };

                var result = importService.Import(importFile, options);

                // 7. Handle result
                if (result.IsSuccess)
                {
                    Princ($"Import complete: {result.ImportedCount} rows imported.");
                    ShowSuccess(result);

                    // Refresh the item editor UI if it's open
                    Princ("Product list updated. Reload the item to see changes.");
                }
                else if (result.WasCancelled)
                {
                    Princ("Import was cancelled by user.");
                    MessageBox.Show("Import was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ShowError($"Import failed: {result.ErrorMessage}");
                }
                */
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("ImportProductList", ex);
            }
        }

        /// <summary>
        /// Import price list data from CSV into the currently selected price list.
        /// CSV must contain: DatabaseId, Cost columns, plus optional DiscountCode, Units, Status columns.
        /// Note: You must have a price list selected in the Database Editor before running this command.
        /// </summary>
        [CommandMethod("ImportPriceList")]
        public static void ImportPriceList()
        {
            try
            {
                // 1. Validate environment
                if (!ValidateFabricationLoaded())
                    return;

                // Check if there's a price list selected
                // Note: This requires access to the currently selected price list from the UI
                // For now, we'll prompt the user to select one
                MessageBox.Show(
                    "Price List Import:\n\n" +
                    "This command requires a price list to be selected.\n\n" +
                    "Please use the Database Editor to select a price list,\n" +
                    "then use the Import button in that interface.\n\n" +
                    "This command will be enhanced in a future update to support\n" +
                    "direct price list selection.",
                    "Price List Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Princ("Price list import: Use Database Editor Import button for now.");

                // TODO: Implement price list selection dialog
                // For now, this is a placeholder that directs users to the UI

                return;
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("ImportPriceList", ex);
            }
        }

        /// <summary>
        /// Opens the Profile Data Copy window to copy database configuration from another Fabrication profile.
        /// Copies .map files from a source profile to the current profile with automatic backup.
        /// </summary>
        [CommandMethod("ImportProfileData")]
        public static void ImportProfileData()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                Princ("Opening Profile Data Copy...");

                var window = new ProfileDataCopyWindow();
                var result = window.ShowDialog();

                if (result == true)
                {
                    Princ("Profile data copy completed. Please restart AutoCAD for changes to take effect.");
                }
                else
                {
                    Princ("Profile data copy cancelled.");
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"Unexpected error: {ex.Message}");
                LogError("ImportProfileData", ex);
            }
        }

        #endregion
    }
}
