using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FabricationSample.ProfileCopy.Utilities;

namespace FabricationSample.ProfileCopy.Services
{
    /// <summary>
    /// Creates and restores ZIP backups of the DATABASE folder.
    /// </summary>
    public class BackupService
    {
        /// <summary>
        /// Creates a ZIP backup of the specified DATABASE folder.
        /// </summary>
        /// <param name="databasePath">Full path to the DATABASE folder to back up.</param>
        /// <returns>Full path to the created backup ZIP file.</returns>
        public string CreateBackup(string databasePath)
        {
            if (!Directory.Exists(databasePath))
                throw new DirectoryNotFoundException($"DATABASE folder not found: {databasePath}");

            string backupDir = ProfilePathHelper.GetBackupDirectory();
            ProfilePathHelper.EnsureDirectoryExists(backupDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string profileName = new DirectoryInfo(databasePath).Parent?.Name ?? "Unknown";
            string backupFileName = $"Backup_{profileName}_{timestamp}.zip";
            string backupPath = Path.Combine(backupDir, backupFileName);

            ZipFile.CreateFromDirectory(databasePath, backupPath, CompressionLevel.Fastest, includeBaseDirectory: true);

            return backupPath;
        }

        /// <summary>
        /// Restores a backup ZIP file to the specified target path.
        /// The target DATABASE folder is cleared and replaced with backup contents.
        /// </summary>
        /// <param name="backupPath">Path to the backup ZIP file.</param>
        /// <param name="targetDatabasePath">Path to the DATABASE folder to restore to.</param>
        public void RestoreBackup(string backupPath, string targetDatabasePath)
        {
            if (!File.Exists(backupPath))
                throw new FileNotFoundException($"Backup file not found: {backupPath}");

            // Extract to a temp directory first
            string tempDir = Path.Combine(Path.GetTempPath(), "FabricationSample_Restore_" + Guid.NewGuid().ToString("N"));

            try
            {
                ZipFile.ExtractToDirectory(backupPath, tempDir);

                // The ZIP contains a "DATABASE" folder inside it
                string extractedDbPath = Path.Combine(tempDir, "DATABASE");
                if (!Directory.Exists(extractedDbPath))
                {
                    // Fallback: maybe the files are directly in tempDir
                    extractedDbPath = tempDir;
                }

                // Copy files from extracted backup back to target
                foreach (var file in Directory.GetFiles(extractedDbPath, "*.*", SearchOption.AllDirectories))
                {
                    string relativePath = file.Substring(extractedDbPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    string targetFile = Path.Combine(targetDatabasePath, relativePath);

                    string targetDir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    File.Copy(file, targetFile, overwrite: true);
                }
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        /// <summary>
        /// Removes old backups, keeping only the most recent ones.
        /// </summary>
        /// <param name="keepCount">Number of recent backups to keep.</param>
        public void CleanOldBackups(int keepCount = 10)
        {
            string backupDir = ProfilePathHelper.GetBackupDirectory();
            if (!Directory.Exists(backupDir))
                return;

            var backupFiles = Directory.GetFiles(backupDir, "Backup_*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToArray();

            if (backupFiles.Length <= keepCount)
                return;

            foreach (var oldFile in backupFiles.Skip(keepCount))
            {
                try
                {
                    oldFile.Delete();
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}
