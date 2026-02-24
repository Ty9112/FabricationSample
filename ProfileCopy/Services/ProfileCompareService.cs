using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Utilities;

namespace FabricationSample.ProfileCopy.Services
{
    /// <summary>
    /// Compares .MAP file contents between two Fabrication profiles.
    /// </summary>
    public class ProfileCompareService
    {
        /// <summary>
        /// Compares two profiles by examining their .MAP files.
        /// Returns a list of diff results for each shared .MAP file.
        /// </summary>
        public List<ProfileDiffResult> Compare(string leftDatabasePath, string rightDatabasePath)
        {
            var results = new List<ProfileDiffResult>();

            if (string.IsNullOrEmpty(leftDatabasePath) || string.IsNullOrEmpty(rightDatabasePath))
                return results;

            if (!Directory.Exists(leftDatabasePath) || !Directory.Exists(rightDatabasePath))
                return results;

            // Get all .MAP files from both profiles
            var leftFiles = GetMapFiles(leftDatabasePath);
            var rightFiles = GetMapFiles(rightDatabasePath);

            // Union of all file names
            var allFileNames = new HashSet<string>(
                leftFiles.Keys.Concat(rightFiles.Keys),
                StringComparer.OrdinalIgnoreCase);

            foreach (string fileName in allFileNames.OrderBy(f => f))
            {
                bool inLeft = leftFiles.ContainsKey(fileName);
                bool inRight = rightFiles.ContainsKey(fileName);

                var diff = new ProfileDiffResult
                {
                    FileName = fileName,
                    DataType = DataTypeDescriptor.GetDisplayNameForFile(fileName)
                };

                if (inLeft && inRight)
                {
                    CompareMapFiles(leftFiles[fileName], rightFiles[fileName], diff);
                }
                else if (inLeft && !inRight)
                {
                    diff.Status = DiffStatus.OnlyLeft;
                    diff.LeftCount = CountLines(leftFiles[fileName]);
                    diff.RightCount = 0;
                    PopulateFileMetadata(leftFiles[fileName], null, diff);
                }
                else
                {
                    diff.Status = DiffStatus.OnlyRight;
                    diff.LeftCount = 0;
                    diff.RightCount = CountLines(rightFiles[fileName]);
                    PopulateFileMetadata(null, rightFiles[fileName], diff);
                }

                results.Add(diff);
            }

            return results;
        }

        /// <summary>
        /// Applies selected .MAP files from the left (source) profile to the right (target) profile.
        /// </summary>
        public int ApplySelected(string leftDatabasePath, string rightDatabasePath, List<ProfileDiffResult> selectedFiles)
        {
            int copiedCount = 0;
            foreach (var diff in selectedFiles)
            {
                try
                {
                    string sourcePath = Path.Combine(leftDatabasePath, diff.FileName);
                    string targetPath = Path.Combine(rightDatabasePath, diff.FileName);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, targetPath, true);
                        copiedCount++;
                    }
                }
                catch { }
            }
            return copiedCount;
        }

        private Dictionary<string, string> GetMapFiles(string databasePath)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in Directory.GetFiles(databasePath, "*.MAP"))
                {
                    files[Path.GetFileName(file)] = file;
                }
                foreach (var file in Directory.GetFiles(databasePath, "*.map"))
                {
                    string name = Path.GetFileName(file);
                    if (!files.ContainsKey(name))
                        files[name] = file;
                }
            }
            catch { }
            return files;
        }

        private void CompareMapFiles(string leftPath, string rightPath, ProfileDiffResult diff)
        {
            try
            {
                var leftInfo = new FileInfo(leftPath);
                var rightInfo = new FileInfo(rightPath);

                diff.LeftSize = leftInfo.Length;
                diff.RightSize = rightInfo.Length;
                PopulateFileMetadata(leftPath, rightPath, diff);

                // Binary comparison first for quick identical check
                if (leftInfo.Length == rightInfo.Length)
                {
                    byte[] leftBytes = File.ReadAllBytes(leftPath);
                    byte[] rightBytes = File.ReadAllBytes(rightPath);

                    if (leftBytes.SequenceEqual(rightBytes))
                    {
                        diff.Status = DiffStatus.Identical;
                        diff.LeftCount = CountLines(leftPath);
                        diff.RightCount = diff.LeftCount;
                        return;
                    }
                }

                // Files differ - do line-level comparison
                var leftLines = ReadFileLines(leftPath);
                var rightLines = ReadFileLines(rightPath);

                diff.LeftCount = leftLines.Count;
                diff.RightCount = rightLines.Count;

                var leftSet = new HashSet<string>(leftLines);
                var rightSet = new HashSet<string>(rightLines);

                diff.AddedCount = rightLines.Count(l => !leftSet.Contains(l));
                diff.RemovedCount = leftLines.Count(l => !rightSet.Contains(l));
                diff.ModifiedCount = 0; // Line-level diff doesn't track modifications per-line

                if (diff.AddedCount == 0 && diff.RemovedCount == 0)
                    diff.Status = DiffStatus.Identical;
                else
                    diff.Status = DiffStatus.Modified;
            }
            catch
            {
                diff.Status = DiffStatus.Error;
            }
        }

        private void PopulateFileMetadata(string leftPath, string rightPath, ProfileDiffResult diff)
        {
            try
            {
                if (leftPath != null && File.Exists(leftPath))
                {
                    var fi = new FileInfo(leftPath);
                    diff.LeftLastModified = fi.LastWriteTime;
                    diff.LeftHash = ComputeMD5(leftPath);
                }
                if (rightPath != null && File.Exists(rightPath))
                {
                    var fi = new FileInfo(rightPath);
                    diff.RightLastModified = fi.LastWriteTime;
                    diff.RightHash = ComputeMD5(rightPath);
                }
            }
            catch { }
        }

        private static string ComputeMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private List<string> ReadFileLines(string path)
        {
            try
            {
                // .MAP files can be UTF-16 or ASCII, try both
                var lines = File.ReadAllLines(path, System.Text.Encoding.Unicode)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                if (lines.Count == 0)
                {
                    lines = File.ReadAllLines(path, System.Text.Encoding.Default)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }

                return lines;
            }
            catch
            {
                return new List<string>();
            }
        }

        private int CountLines(string path)
        {
            try
            {
                return File.ReadAllLines(path).Length;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class ProfileDiffResult : INotifyPropertyChanged
    {
        private bool _isApplySelected;

        public string FileName { get; set; }
        public string DataType { get; set; }
        public int LeftCount { get; set; }
        public int RightCount { get; set; }
        public int AddedCount { get; set; }
        public int RemovedCount { get; set; }
        public int ModifiedCount { get; set; }
        public long LeftSize { get; set; }
        public long RightSize { get; set; }
        public DateTime? LeftLastModified { get; set; }
        public DateTime? RightLastModified { get; set; }
        public string LeftHash { get; set; }
        public string RightHash { get; set; }
        public DiffStatus Status { get; set; }

        public string LeftModifiedDisplay => LeftLastModified?.ToString("yyyy-MM-dd HH:mm") ?? "";
        public string RightModifiedDisplay => RightLastModified?.ToString("yyyy-MM-dd HH:mm") ?? "";
        public string LeftHashShort => string.IsNullOrEmpty(LeftHash) ? "" : LeftHash.Substring(0, 8);
        public string RightHashShort => string.IsNullOrEmpty(RightHash) ? "" : RightHash.Substring(0, 8);

        public bool IsApplySelected
        {
            get { return _isApplySelected; }
            set
            {
                _isApplySelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsApplySelected"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string StatusDisplay
        {
            get
            {
                switch (Status)
                {
                    case DiffStatus.Identical: return "Identical";
                    case DiffStatus.Modified: return "Modified";
                    case DiffStatus.OnlyLeft: return "Only in Left";
                    case DiffStatus.OnlyRight: return "Only in Right";
                    case DiffStatus.Error: return "Error";
                    default: return "Unknown";
                }
            }
        }

        public string SizeDiff
        {
            get
            {
                if (Status == DiffStatus.Identical) return "-";
                long diff = RightSize - LeftSize;
                if (diff > 0) return $"+{diff:N0} bytes";
                if (diff < 0) return $"{diff:N0} bytes";
                return "Same size";
            }
        }
    }

    public enum DiffStatus
    {
        Identical,
        Modified,
        OnlyLeft,
        OnlyRight,
        Error
    }
}
