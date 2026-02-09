using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

using Autodesk.Fabrication.Content;

using FabricationSample.ContentTransfer.Models;
using FabricationSample.ContentTransfer.Services;
using FabricationSample.ContentTransfer.Windows;
using FabricationSample.Manager;

using MessageBox = System.Windows.MessageBox;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Content Transfer (Export/Import) functionality
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        #region Export

        private void btnExportContent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportWindow = new ItemExportWindow();
                exportWindow.Owner = Window.GetWindow(this);
                bool? result = exportWindow.ShowDialog();

                if (result != true)
                    return;

                var selectedPaths = exportWindow.SelectedItemPaths;
                var outputFolder = exportWindow.OutputFolder;

                if (selectedPaths == null || selectedPaths.Count == 0 || string.IsNullOrEmpty(outputFolder))
                    return;

                var exportService = new ItemContentExportService();
                var package = exportService.ExportItems(selectedPaths, outputFolder);

                MessageBox.Show(
                    $"Exported {package.Items.Count} item{(package.Items.Count == 1 ? "" : "s")} to:\n{outputFolder}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Import

        private void btnImportContent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Select import folder
                string importFolder;
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the import content folder (containing manifest.json)";
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;
                    importFolder = dialog.SelectedPath;
                }

                // Load manifest
                var importService = new ItemContentImportService();
                var package = importService.LoadPackage(importFolder);

                if (package == null)
                {
                    MessageBox.Show(
                        "No manifest.json found in the selected folder.\n\nPlease select a folder created by the Export Items function.",
                        "Invalid Package",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (package.Items.Count == 0)
                {
                    MessageBox.Show(
                        "The package contains no items.",
                        "Empty Package",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Validate references against current database
                var validationResults = importService.ValidatePackage(package);

                // Show import preview window
                var importWindow = new ItemImportWindow(package, importFolder, validationResults);
                importWindow.Owner = Window.GetWindow(this);
                bool? result = importWindow.ShowDialog();

                if (result != true)
                    return;

                var selectedIndices = importWindow.SelectedIndices;
                var targetFolder = importWindow.TargetFolderPath;
                var overrides = importWindow.OverridesPerItem;

                if (selectedIndices == null || selectedIndices.Count == 0 || string.IsNullOrEmpty(targetFolder))
                    return;

                // Check for duplicate DatabaseIds in the target folder
                var duplicates = importService.CheckDuplicateDatabaseIds(package, targetFolder);
                if (duplicates.Count > 0)
                {
                    var dupMessage = $"The following items have Database IDs that already exist in the target folder:\n";
                    foreach (var dup in duplicates.Take(10))
                    {
                        dupMessage += $"\n  - {dup.ImportFileName} (ID: {dup.DatabaseId}) conflicts with {Path.GetFileName(dup.ExistingFilePath)}";
                    }
                    if (duplicates.Count > 10)
                        dupMessage += $"\n  ... and {duplicates.Count - 10} more";

                    dupMessage += "\n\nDuplicate Database IDs can cause conflicts. Continue anyway?";

                    var dupResult = MessageBox.Show(dupMessage, "Duplicate Database IDs Found",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (dupResult != MessageBoxResult.Yes)
                        return;
                }

                // Perform import with overrides
                var importResults = importService.ImportItems(package, importFolder, targetFolder, selectedIndices, overrides);

                // Show results
                ShowImportResults(importResults);

                // Refresh tree - add imported items to the tree view
                RefreshTreeAfterImport(importResults, targetFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Import failed: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowImportResults(List<ItemImportResult> results)
        {
            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);
            var allWarnings = results.SelectMany(r => r.Warnings).ToList();
            var allErrors = results.SelectMany(r => r.Errors).ToList();

            var message = $"Imported {successCount} item{(successCount == 1 ? "" : "s")}.";

            if (failCount > 0)
                message += $"\n{failCount} item{(failCount == 1 ? "" : "s")} failed.";

            if (allWarnings.Count > 0)
            {
                // Group warnings to avoid duplicates
                var uniqueWarnings = allWarnings.Distinct().ToList();
                message += $"\n\n{uniqueWarnings.Count} warning{(uniqueWarnings.Count == 1 ? "" : "s")}:";
                foreach (var warning in uniqueWarnings.Take(10))
                    message += $"\n  - {warning}";
                if (uniqueWarnings.Count > 10)
                    message += $"\n  ... and {uniqueWarnings.Count - 10} more";
            }

            if (allErrors.Count > 0)
            {
                message += $"\n\nErrors:";
                foreach (var error in allErrors.Take(5))
                    message += $"\n  - {error}";
            }

            var icon = failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(message, "Import Results", MessageBoxButton.OK, icon);
        }

        private void RefreshTreeAfterImport(List<ItemImportResult> results, string targetFolder)
        {
            try
            {
                // Find the tree view item for the target folder
                var treeView = FabricationManager.ItemFoldersView?.trvItemFolders;
                if (treeView == null)
                    return;

                TreeViewItem targetTreeItem = FindTreeViewItemForFolder(treeView.Items, targetFolder);
                if (targetTreeItem == null)
                    return;

                // Add successfully imported items to the tree
                foreach (var result in results.Where(r => r.Success))
                {
                    string fullPath = Path.Combine(targetFolder, result.FileName);
                    FabricationManager.ItemFoldersView.AddNewTreeViewItem(
                        targetTreeItem,
                        ItemFoldersView.TreeViewNodeType.file,
                        fullPath);
                }
            }
            catch { }
        }

        private TreeViewItem FindTreeViewItemForFolder(ItemCollection items, string folderPath)
        {
            foreach (var item in items)
            {
                if (item is TreeViewItem treeItem)
                {
                    if (treeItem.Tag is ItemFolder folder)
                    {
                        if (string.Equals(folder.Directory, folderPath, StringComparison.OrdinalIgnoreCase))
                            return treeItem;

                        // Check children
                        var found = FindTreeViewItemForFolder(treeItem.Items, folderPath);
                        if (found != null)
                            return found;
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
