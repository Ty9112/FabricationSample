using System;
using System.Collections.Generic;
using System.Text;

namespace FabricationSample.ProfileCopy.Models
{
    /// <summary>
    /// Result of a profile data copy operation.
    /// </summary>
    public class CopyResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string BackupPath { get; set; }
        public List<string> CopiedFiles { get; set; } = new List<string>();
        public List<string> SkippedFiles { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Returns a user-friendly summary of the copy operation.
        /// </summary>
        public string GetSummary()
        {
            var sb = new StringBuilder();

            if (Success)
            {
                sb.AppendLine("Profile data copy completed successfully.");
                sb.AppendLine();
                sb.AppendLine($"Files copied: {CopiedFiles.Count}");
                foreach (var file in CopiedFiles)
                    sb.AppendLine($"  - {file}");

                if (SkippedFiles.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Files skipped (not found in source): {SkippedFiles.Count}");
                    foreach (var file in SkippedFiles)
                        sb.AppendLine($"  - {file}");
                }

                if (!string.IsNullOrEmpty(BackupPath))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Backup saved to: {BackupPath}");
                }

                sb.AppendLine();
                sb.AppendLine($"Duration: {Duration.TotalSeconds:F1} seconds");
                sb.AppendLine();
                sb.AppendLine("IMPORTANT: You must restart AutoCAD for changes to take effect.");
            }
            else
            {
                sb.AppendLine("Profile data copy failed.");
                sb.AppendLine();
                sb.AppendLine($"Error: {ErrorMessage}");

                if (!string.IsNullOrEmpty(BackupPath))
                {
                    sb.AppendLine();
                    sb.AppendLine($"A backup was created before the error at: {BackupPath}");
                    sb.AppendLine("The backup has been restored automatically.");
                }
            }

            return sb.ToString();
        }
    }
}
