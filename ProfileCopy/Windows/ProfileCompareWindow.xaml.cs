using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Services;

namespace FabricationSample.ProfileCopy.Windows
{
    /// <summary>
    /// Window for comparing two Fabrication profiles side-by-side.
    /// Shows differences in .MAP files between two selected profiles.
    /// </summary>
    public partial class ProfileCompareWindow : Window
    {
        private readonly List<ProfileInfo> _profiles;
        private readonly ProfileCompareService _compareService;
        private List<ProfileDiffResult> _currentResults;

        public ProfileCompareWindow(List<ProfileInfo> profiles)
        {
            InitializeComponent();
            _profiles = profiles ?? new List<ProfileInfo>();
            _compareService = new ProfileCompareService();

            cmbProfileLeft.ItemsSource = _profiles;
            cmbProfileRight.ItemsSource = _profiles;

            // Pre-select first two profiles if available
            if (_profiles.Count >= 1)
                cmbProfileLeft.SelectedIndex = 0;
            if (_profiles.Count >= 2)
                cmbProfileRight.SelectedIndex = 1;
        }

        private void cmbProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset results when selection changes
            btnCompare.IsEnabled = cmbProfileLeft.SelectedItem != null && cmbProfileRight.SelectedItem != null;
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            var left = cmbProfileLeft.SelectedItem as ProfileInfo;
            var right = cmbProfileRight.SelectedItem as ProfileInfo;

            if (left == null || right == null)
            {
                MessageBox.Show("Please select both profiles.", "Missing Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (left.DatabasePath == right.DatabasePath)
            {
                MessageBox.Show("Please select two different profiles.", "Same Profile",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtCompareStatus.Text = "Comparing...";
            btnCompare.IsEnabled = false;

            try
            {
                _currentResults = _compareService.Compare(left.DatabasePath, right.DatabasePath);
                dgCompareResults.ItemsSource = _currentResults;

                int identical = _currentResults.Count(r => r.Status == DiffStatus.Identical);
                int modified = _currentResults.Count(r => r.Status == DiffStatus.Modified);
                int onlyLeft = _currentResults.Count(r => r.Status == DiffStatus.OnlyLeft);
                int onlyRight = _currentResults.Count(r => r.Status == DiffStatus.OnlyRight);

                txtCompareStatus.Text = $"Compared {_currentResults.Count} file(s).";
                txtCompareSummary.Text = $"Identical: {identical}  |  Modified: {modified}  |  Only in {left.Name}: {onlyLeft}  |  Only in {right.Name}: {onlyRight}";

                btnSelectAllModified.IsEnabled = modified > 0 || onlyLeft > 0;
                btnApplySelected.IsEnabled = true;
                btnExportReport.IsEnabled = true;
                txtApplyStatus.Text = "";
            }
            catch (Exception ex)
            {
                txtCompareStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btnCompare.IsEnabled = true;
            }
        }

        private void btnSelectAllModified_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResults == null) return;

            foreach (var r in _currentResults)
            {
                r.IsApplySelected = (r.Status == DiffStatus.Modified || r.Status == DiffStatus.OnlyLeft);
            }
        }

        private void btnApplySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResults == null) return;

            var left = cmbProfileLeft.SelectedItem as ProfileInfo;
            var right = cmbProfileRight.SelectedItem as ProfileInfo;
            if (left == null || right == null) return;

            var selected = _currentResults.Where(r => r.IsApplySelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No files selected. Check the 'Apply' column for files you want to copy.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string fileList = string.Join("\n", selected.Select(s => $"  {s.FileName} ({s.StatusDisplay})"));
            var confirm = MessageBox.Show(
                $"Copy {selected.Count} file(s) from {left.Name} to {right.Name}?\n\n{fileList}\n\nA backup of the target profile will NOT be created automatically. Proceed?",
                "Confirm Apply", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int copied = _compareService.ApplySelected(left.DatabasePath, right.DatabasePath, selected);
                txtApplyStatus.Text = $"Applied {copied} file(s).";

                // Re-run compare to show updated status
                _currentResults = _compareService.Compare(left.DatabasePath, right.DatabasePath);
                dgCompareResults.ItemsSource = _currentResults;

                int identical = _currentResults.Count(r => r.Status == DiffStatus.Identical);
                int modified = _currentResults.Count(r => r.Status == DiffStatus.Modified);
                txtCompareSummary.Text = $"Identical: {identical}  |  Modified: {modified}  |  After apply: {copied} file(s) copied";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying files: {ex.Message}", "Apply Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentResults == null || _currentResults.Count == 0) return;

            var left = cmbProfileLeft.SelectedItem as ProfileInfo;
            var right = cmbProfileRight.SelectedItem as ProfileInfo;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"ProfileCompare_{left?.Name ?? "Left"}_{right?.Name ?? "Right"}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Profile Compare Report");
                sb.AppendLine($"# Left: {left?.Name ?? "Unknown"} ({left?.DatabasePath ?? ""})");
                sb.AppendLine($"# Right: {right?.Name ?? "Unknown"} ({right?.DatabasePath ?? ""})");
                sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("File,Data Type,Status,Left Lines,Right Lines,Added,Removed,Size Diff,Left Modified,Right Modified,Left Hash,Right Hash");

                foreach (var r in _currentResults)
                {
                    sb.Append(CsvEscape(r.FileName)); sb.Append(',');
                    sb.Append(CsvEscape(r.DataType)); sb.Append(',');
                    sb.Append(CsvEscape(r.StatusDisplay)); sb.Append(',');
                    sb.Append(r.LeftCount); sb.Append(',');
                    sb.Append(r.RightCount); sb.Append(',');
                    sb.Append(r.AddedCount); sb.Append(',');
                    sb.Append(r.RemovedCount); sb.Append(',');
                    sb.Append(CsvEscape(r.SizeDiff)); sb.Append(',');
                    sb.Append(CsvEscape(r.LeftModifiedDisplay)); sb.Append(',');
                    sb.Append(CsvEscape(r.RightModifiedDisplay)); sb.Append(',');
                    sb.Append(r.LeftHash ?? ""); sb.Append(',');
                    sb.Append(r.RightHash ?? "");
                    sb.AppendLine();
                }

                File.WriteAllText(dlg.FileName, sb.ToString());
                MessageBox.Show($"Report exported to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
