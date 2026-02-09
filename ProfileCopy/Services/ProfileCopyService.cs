using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Utilities;

namespace FabricationSample.ProfileCopy.Services
{
    /// <summary>
    /// Orchestrates the profile data copy operation: backup, copy .map files, handle errors.
    /// </summary>
    public class ProfileCopyService
    {
        private readonly BackupService _backupService;

        public ProfileCopyService()
        {
            _backupService = new BackupService();
        }

        /// <summary>
        /// Event raised to report progress during the copy operation.
        /// </summary>
        public event EventHandler<CopyProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Copies selected .map files from the source profile to the current profile's DATABASE folder.
        /// </summary>
        /// <param name="sourceProfile">Source profile to copy from.</param>
        /// <param name="targetDatabasePath">Target DATABASE folder path (current profile).</param>
        /// <param name="options">Copy options including backup preference and selected data types.</param>
        /// <returns>Result of the copy operation.</returns>
        public CopyResult CopyData(ProfileInfo sourceProfile, string targetDatabasePath, MergeOptions options)
        {
            var result = new CopyResult();
            var sw = Stopwatch.StartNew();

            if (sourceProfile == null || !sourceProfile.IsValid())
            {
                result.Success = false;
                result.ErrorMessage = "Invalid source profile.";
                result.Duration = sw.Elapsed;
                return result;
            }

            if (string.IsNullOrEmpty(targetDatabasePath) || !Directory.Exists(targetDatabasePath))
            {
                result.Success = false;
                result.ErrorMessage = "Target DATABASE folder not found.";
                result.Duration = sw.Elapsed;
                return result;
            }

            var selectedTypes = options.SelectedDataTypes.Where(d => d.IsSelected && d.IsAvailable).ToList();
            if (selectedTypes.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "No data types selected for copying.";
                result.Duration = sw.Elapsed;
                return result;
            }

            // Step 1: Create backup if requested
            if (options.CreateBackup)
            {
                try
                {
                    ReportProgress("Creating backup of current DATABASE folder...", 0, selectedTypes.Count + 1);
                    result.BackupPath = _backupService.CreateBackup(targetDatabasePath);
                    _backupService.CleanOldBackups();
                    ReportProgress($"Backup created: {Path.GetFileName(result.BackupPath)}", 1, selectedTypes.Count + 1);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to create backup: {ex.Message}";
                    result.Duration = sw.Elapsed;
                    return result;
                }
            }

            // Step 2: Copy each selected .map file
            try
            {
                int startStep = options.CreateBackup ? 1 : 0;
                int totalSteps = selectedTypes.Count + startStep;

                for (int i = 0; i < selectedTypes.Count; i++)
                {
                    var dataType = selectedTypes[i];
                    string sourcePath = ProfilePathHelper.GetMapFilePath(sourceProfile.DatabasePath, dataType.FileName);
                    string targetPath = ProfilePathHelper.GetMapFilePath(targetDatabasePath, dataType.FileName);

                    ReportProgress($"Copying {dataType.DisplayName} ({dataType.FileName})...",
                        startStep + i, totalSteps);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, targetPath, overwrite: true);
                        result.CopiedFiles.Add(dataType.FileName);
                    }
                    else
                    {
                        result.SkippedFiles.Add(dataType.FileName);
                    }
                }

                result.Success = true;
                ReportProgress("Copy complete.", totalSteps, totalSteps);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error during copy: {ex.Message}";

                // Attempt to restore from backup on failure
                if (!string.IsNullOrEmpty(result.BackupPath))
                {
                    try
                    {
                        ReportProgress("Error occurred. Restoring from backup...", 0, 1);
                        _backupService.RestoreBackup(result.BackupPath, targetDatabasePath);
                        ReportProgress("Backup restored successfully.", 1, 1);
                        result.ErrorMessage += "\n\nThe backup has been restored automatically.";
                    }
                    catch (Exception restoreEx)
                    {
                        result.ErrorMessage += $"\n\nWARNING: Failed to restore backup: {restoreEx.Message}" +
                            $"\nBackup file is at: {result.BackupPath}";
                    }
                }
            }

            result.Duration = sw.Elapsed;
            return result;
        }

        private void ReportProgress(string message, int current, int total)
        {
            ProgressChanged?.Invoke(this, new CopyProgressEventArgs
            {
                Message = message,
                Current = current,
                Total = total
            });
        }
    }

    /// <summary>
    /// Event args for copy progress reporting.
    /// </summary>
    public class CopyProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
        public double PercentComplete => Total > 0 ? (double)Current / Total * 100 : 0;
    }
}
