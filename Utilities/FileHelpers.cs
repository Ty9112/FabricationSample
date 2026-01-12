using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// File operation utilities for export/import workflows.
    /// </summary>
    public static class FileHelpers
    {
        /// <summary>
        /// Prompt user to select an export folder using FolderBrowserDialog.
        /// </summary>
        /// <param name="title">Dialog title describing the export type</param>
        /// <param name="defaultPath">Default folder to show (optional)</param>
        /// <returns>Selected folder path, or null if user cancelled</returns>
        public static string PromptForExportFolder(string title, string defaultPath = null)
        {
            try
            {
                // Use default path or try to get from Fabrication working directory
                if (string.IsNullOrEmpty(defaultPath))
                {
                    defaultPath = GetDefaultExportFolder();
                }

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = title;
                    dialog.ShowNewFolderButton = true;
                    dialog.SelectedPath = defaultPath;

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error selecting folder: {ex.Message}",
                    "Folder Selection Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return null; // User cancelled or error occurred
        }

        /// <summary>
        /// Get default export folder location.
        /// First tries Fabrication working directory parent, then falls back to My Documents.
        /// </summary>
        /// <returns>Default export folder path</returns>
        public static string GetDefaultExportFolder()
        {
            try
            {
                // Try to get Fabrication working directory
                string workingDir = Autodesk.Fabrication.ApplicationServices.Application.WorkingDirectory;
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    // Use parent directory of working directory
                    string parentDir = Path.GetDirectoryName(workingDir);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        return parentDir;
                    }
                }
            }
            catch
            {
                // Fall through to default
            }

            // Fallback to My Documents
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// Generate a timestamped file path for exports.
        /// </summary>
        /// <param name="folder">Folder to create file in</param>
        /// <param name="baseName">Base name for the file (e.g., "ProductInfo")</param>
        /// <param name="extension">File extension (default: .csv)</param>
        /// <param name="timestampFormat">Timestamp format (default: yyyyMMdd_HHmmss)</param>
        /// <returns>Full file path with timestamp</returns>
        public static string GenerateTimestampedFilePath(
            string folder,
            string baseName,
            string extension = ".csv",
            string timestampFormat = "yyyyMMdd_HHmmss")
        {
            if (string.IsNullOrEmpty(folder))
                throw new ArgumentException("Folder cannot be null or empty", nameof(folder));

            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException("Base name cannot be null or empty", nameof(baseName));

            // Ensure extension starts with dot
            if (!extension.StartsWith("."))
                extension = "." + extension;

            // Sanitize base name
            baseName = CsvHelpers.SanitizeFileName(baseName);

            // Generate timestamp
            string timestamp = DateTime.Now.ToString(timestampFormat);

            // Construct file name
            string fileName = $"{baseName}_{timestamp}{extension}";

            return Path.Combine(folder, fileName);
        }

        /// <summary>
        /// Create a timestamped subfolder for batch exports.
        /// </summary>
        /// <param name="parentFolder">Parent folder</param>
        /// <param name="folderBaseName">Base name for the folder</param>
        /// <param name="timestampFormat">Timestamp format (default: yyyyMMdd_HHmmss)</param>
        /// <returns>Path to created subfolder</returns>
        public static string CreateTimestampedFolder(
            string parentFolder,
            string folderBaseName,
            string timestampFormat = "yyyyMMdd_HHmmss")
        {
            if (string.IsNullOrEmpty(parentFolder))
                throw new ArgumentException("Parent folder cannot be null or empty", nameof(parentFolder));

            if (string.IsNullOrEmpty(folderBaseName))
                throw new ArgumentException("Folder base name cannot be null or empty", nameof(folderBaseName));

            // Sanitize folder name
            folderBaseName = CsvHelpers.SanitizeFileName(folderBaseName);

            // Generate timestamp
            string timestamp = DateTime.Now.ToString(timestampFormat);

            // Construct folder path
            string folderPath = Path.Combine(parentFolder, $"{folderBaseName}_{timestamp}");

            // Create directory
            Directory.CreateDirectory(folderPath);

            return folderPath;
        }

        /// <summary>
        /// Open a file in Windows Explorer (or default application).
        /// </summary>
        /// <param name="filePath">Path to file to open</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool OpenFileInExplorer(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    MessageBox.Show(
                        $"File not found: {filePath}",
                        "File Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                Process.Start("explorer.exe", filePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open file: {ex.Message}",
                    "Open File Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Open a folder in Windows Explorer.
        /// </summary>
        /// <param name="folderPath">Path to folder to open</param>
        /// <returns>True if successful, false otherwise</returns>
        public static bool OpenFolderInExplorer(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                {
                    MessageBox.Show(
                        $"Folder not found: {folderPath}",
                        "Folder Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                Process.Start("explorer.exe", folderPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open folder: {ex.Message}",
                    "Open Folder Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Ensure a directory exists, creating it if necessary.
        /// </summary>
        /// <param name="directoryPath">Path to directory</param>
        /// <returns>True if directory exists or was created successfully</returns>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (string.IsNullOrEmpty(directoryPath))
                    return false;

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a file path is writable.
        /// </summary>
        /// <param name="filePath">Path to check</param>
        /// <returns>True if writable, false otherwise</returns>
        public static bool IsFilePathWritable(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return false;

                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                    return false;

                // Try to create a test file
                string testFile = Path.Combine(directory, $"_test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
