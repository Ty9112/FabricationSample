using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Services;
using FabricationSample.ProfileCopy.Utilities;
using FabricationSample.ProfileCopy.Windows;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Profiles tab functionality.
    /// Allows copying .MAP files from one Fabrication profile to the current one.
    /// </summary>
    public partial class DatabaseEditor : System.Windows.Controls.UserControl
    {
        private ProfileDiscoveryService _profileDiscoveryService;
        private ProfileCopyService _profileCopyService;
        private BackupService _backupService;
        private ProfileManifestService _manifestService;
        private SelectiveCleanupService _cleanupService;
        private DataTypeDescriptor[] _profileDataTypes;
        private ProfileInfo _selectedSourceProfile;
        private string _currentProfileDatabasePath;
        private bool _isProfileCopying;
        private List<CheckBox> _pushTargetCheckBoxes = new List<CheckBox>();
        private bool _isPushInProgress;

        private void tbiProfiles_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_profileDiscoveryService == null)
                {
                    _profileDiscoveryService = new ProfileDiscoveryService();
                    _profileCopyService = new ProfileCopyService();
                    _backupService = new BackupService();
                    _manifestService = new ProfileManifestService();
                    _cleanupService = new SelectiveCleanupService();
                }

                // Get current database path from Fabrication API
                _currentProfileDatabasePath = _profileDiscoveryService.GetCurrentDatabasePath();

                if (string.IsNullOrEmpty(_currentProfileDatabasePath))
                {
                    txtProfileCurrentInfo.Text = "No active profile detected.";
                    return;
                }

                // Show current profile info
                string currentProfile = _profileDiscoveryService.GetCurrentProfileName();
                bool onGlobal = string.IsNullOrEmpty(currentProfile)
                    || currentProfile.Equals("Global", StringComparison.OrdinalIgnoreCase);
                string displayName = onGlobal ? "Global" : currentProfile;
                txtProfileCurrentInfo.Text = $"Current Profile: {displayName}";

                // Resolve the actual destination path for copy operations.
                // If on a named profile, destination = {root}\profiles\{Name}\DATABASE
                // If on Global, destination = {root}\DATABASE
                string dbRoot = ProfileCopy.Utilities.ProfilePathHelper.GetDatabaseRoot(_currentProfileDatabasePath);
                if (!onGlobal && dbRoot != null)
                {
                    _currentProfileDatabasePath = System.IO.Path.Combine(
                        dbRoot, "profiles", currentProfile, "DATABASE");
                }
                txtProfileCurrentPath.Text = _currentProfileDatabasePath;

                // Check for pending selective cleanup from a previous copy.
                // Check legacy path (backup directory) first, then profile-specific path.
                try
                {
                    if (_cleanupService.HasPendingCleanup())
                    {
                        string cleanupResult = _cleanupService.ExecutePendingCleanup();
                        if (cleanupResult != null)
                        {
                            MessageBox.Show(cleanupResult, "Selective Cleanup",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception cleanupEx)
                {
                    MessageBox.Show(
                        $"Error during selective cleanup:\n\n{cleanupEx.Message}\n\nThe cleanup file will be removed.",
                        "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    try { File.Delete(_cleanupService.GetCleanupFilePath()); } catch { }
                }

                try
                {
                    if (_cleanupService.HasPendingCleanup(_currentProfileDatabasePath))
                    {
                        string cleanupResult = _cleanupService.ExecutePendingCleanup(_currentProfileDatabasePath);
                        if (cleanupResult != null)
                        {
                            MessageBox.Show(cleanupResult, "Selective Cleanup",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                catch (Exception cleanupEx)
                {
                    MessageBox.Show(
                        $"Error during selective cleanup:\n\n{cleanupEx.Message}\n\nThe cleanup file will be removed.",
                        "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    try { File.Delete(_cleanupService.GetCleanupFilePath(_currentProfileDatabasePath)); } catch { }
                }

                // Generate manifest for current profile (fire-and-forget)
                try
                {
                    _manifestService.GenerateManifest(_currentProfileDatabasePath, displayName);
                }
                catch { }

                // Discover all profiles (Global always first)
                var profiles = _profileDiscoveryService.GetAvailableProfiles(_currentProfileDatabasePath);

                // Show ALL profiles - current one will be greyed out in dropdown
                cmbProfileSource.ItemsSource = profiles;
                txtProfileStatus.Text = $"{profiles.Count} profile(s) found. Select a source to copy from.";

                // Initialize data type descriptors with grouping
                _profileDataTypes = DataTypeDescriptor.GetAllDescriptors();
                lstProfileDataTypes.ItemsSource = _profileDataTypes;

                var view = CollectionViewSource.GetDefaultView(_profileDataTypes);
                if (view.GroupDescriptions.Count == 0)
                    view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));

                // Push to Profiles: show only when on Global
                grpPushToProfiles.Visibility = onGlobal ? Visibility.Visible : Visibility.Collapsed;
                if (onGlobal)
                {
                    PopulatePushTargetProfiles(profiles);

                    // Auto-check data type availability against Global's DATABASE folder
                    // so the user can select data types without picking a source from the dropdown.
                    var globalProfile = profiles.FirstOrDefault(p => p.Name == "Global");
                    if (globalProfile != null)
                    {
                        // Set Global as the selected source so preview clicks and
                        // manifest lookups work without manual dropdown selection.
                        _selectedSourceProfile = globalProfile;
                        cmbProfileSource.SelectedItem = globalProfile;

                        _profileDiscoveryService.CheckAvailableDataTypes(globalProfile, _profileDataTypes);
                        foreach (var dt in _profileDataTypes)
                            dt.IsSelected = dt.IsAvailable;
                        lstProfileDataTypes.Items.Refresh();

                        UpdateManifestItemCounts();
                    }
                }

                // Load available backups
                RefreshBackupList();
            }
            catch (Exception ex)
            {
                txtProfileStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void cmbProfileSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbProfileSource.SelectedItem as ProfileInfo;

            // Reject selection of the current profile (except on Global,
            // where Global is shown as the source for clarity).
            if (selected != null && selected.IsCurrent
                && !selected.Name.Equals("Global", StringComparison.OrdinalIgnoreCase))
            {
                cmbProfileSource.SelectedItem = _selectedSourceProfile;
                return;
            }

            _selectedSourceProfile = selected;
            if (_selectedSourceProfile == null)
            {
                txtProfileSourcePath.Text = "";
                return;
            }

            txtProfileSourcePath.Text = _selectedSourceProfile.DatabasePath;

            // Check which .MAP files exist in the source profile
            _profileDiscoveryService.CheckAvailableDataTypes(_selectedSourceProfile, _profileDataTypes);

            // Load source manifest and update item counts
            UpdateManifestItemCounts();

            // Auto-select all available types and clear previous selective choices
            foreach (var dt in _profileDataTypes)
            {
                dt.IsSelected = dt.IsAvailable;
                dt.SelectedItems = null;
            }

            lstProfileDataTypes.Items.Refresh();
            UpdateProfileGroupCheckBoxStates();
        }

        private void UpdateManifestItemCounts()
        {
            if (_selectedSourceProfile == null || _profileDataTypes == null) return;

            var manifest = _manifestService.LoadManifest(_selectedSourceProfile.DatabasePath);

            foreach (var dt in _profileDataTypes)
            {
                if (dt.IsEnumerable && dt.ManifestKey != null && manifest != null
                    && manifest.DataTypes.ContainsKey(dt.ManifestKey))
                {
                    dt.ManifestItemCount = manifest.DataTypes[dt.ManifestKey].Count;
                }
                else
                {
                    dt.ManifestItemCount = null;
                }
            }
        }

        private void ProfileDataType_Click(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as System.Windows.Controls.TextBlock;
            if (textBlock == null) return;

            var descriptor = textBlock.DataContext as DataTypeDescriptor;
            if (descriptor == null || !descriptor.IsEnumerable) return;

            if (_selectedSourceProfile == null)
            {
                MessageBox.Show("Please select a source profile first.", "No Source",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var manifest = _manifestService.LoadManifest(_selectedSourceProfile.DatabasePath);
            if (manifest == null || !manifest.DataTypes.ContainsKey(descriptor.ManifestKey))
            {
                MessageBox.Show(
                    $"No preview available for {descriptor.DisplayName}.\n\n" +
                    $"Load the \"{_selectedSourceProfile.Name}\" profile with this addin first to generate a manifest.",
                    "No Manifest", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var items = manifest.DataTypes[descriptor.ManifestKey];
            var preview = new DataTypePreviewWindow(
                descriptor.DisplayName,
                _selectedSourceProfile.Name,
                items,
                descriptor.SupportsSelectiveCleanup,
                descriptor.SelectedItems);

            preview.Owner = Window.GetWindow(this);

            if (preview.ShowDialog() == true)
            {
                // If all items selected, clear the selective filter
                if (preview.SelectedItemNames.Count == items.Count)
                {
                    descriptor.SelectedItems = null;
                }
                else
                {
                    descriptor.SelectedItems = preview.SelectedItemNames;
                }
                descriptor.ManifestItemCount = items.Count;
                lstProfileDataTypes.Items.Refresh();
            }
        }

        private void btnProfileSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_profileDataTypes == null) return;
            foreach (var dt in _profileDataTypes.Where(d => d.IsAvailable))
                dt.IsSelected = true;
            lstProfileDataTypes.Items.Refresh();
            UpdateProfileGroupCheckBoxStates();
        }

        private void btnProfileSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (_profileDataTypes == null) return;
            foreach (var dt in _profileDataTypes)
                dt.IsSelected = false;
            lstProfileDataTypes.Items.Refresh();
            UpdateProfileGroupCheckBoxStates();
        }

        private void btnProfileSelectPriceLabor_Click(object sender, RoutedEventArgs e)
        {
            if (_profileDataTypes == null) return;
            foreach (var dt in _profileDataTypes)
                dt.IsSelected = false;
            foreach (var dt in _profileDataTypes.Where(d => d.Group == "Price & Labor" && d.IsAvailable))
                dt.IsSelected = true;
            lstProfileDataTypes.Items.Refresh();
            UpdateProfileGroupCheckBoxStates();
        }

        private void ProfileGroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_profileDataTypes == null) return;
            var cb = sender as CheckBox;
            if (cb == null) return;

            string groupName = cb.Tag as string;
            if (string.IsNullOrEmpty(groupName)) return;

            var groupItems = _profileDataTypes.Where(d => d.Group == groupName).ToList();
            bool allAvailableSelected = groupItems.Where(d => d.IsAvailable).All(d => d.IsSelected);

            if (allAvailableSelected)
            {
                // All selected -> deselect all
                foreach (var dt in groupItems)
                    dt.IsSelected = false;
                cb.IsChecked = false;
            }
            else
            {
                // Not all selected -> select all available
                foreach (var dt in groupItems.Where(d => d.IsAvailable))
                    dt.IsSelected = true;
                cb.IsChecked = groupItems.Any(d => d.IsAvailable);
            }

            lstProfileDataTypes.Items.Refresh();
        }

        private void UpdateProfileGroupCheckBoxStates()
        {
            if (_profileDataTypes == null) return;

            // Find all group header CheckBoxes in the visual tree
            var checkBoxes = FindVisualChildren<CheckBox>(lstProfileDataTypes)
                .Where(cb => cb.Tag is string);

            foreach (var cb in checkBoxes)
            {
                string groupName = cb.Tag as string;
                if (string.IsNullOrEmpty(groupName)) continue;

                var groupItems = _profileDataTypes.Where(d => d.Group == groupName).ToList();
                var availableItems = groupItems.Where(d => d.IsAvailable).ToList();

                if (availableItems.Count == 0)
                {
                    cb.IsChecked = false;
                }
                else
                {
                    int selectedCount = availableItems.Count(d => d.IsSelected);
                    if (selectedCount == 0)
                        cb.IsChecked = false;
                    else if (selectedCount == availableItems.Count)
                        cb.IsChecked = true;
                    else
                        cb.IsChecked = null; // indeterminate
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    yield return t;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private void btnProfileCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_isProfileCopying) return;

            if (_selectedSourceProfile == null)
            {
                MessageBox.Show("Please select a source profile.", "No Source Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedTypes = _profileDataTypes?.Where(d => d.IsSelected && d.IsAvailable).ToList();
            if (selectedTypes == null || selectedTypes.Count == 0)
            {
                MessageBox.Show("Please select at least one data type to copy.", "No Data Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check how many data types have selective items
            var selectiveTypes = selectedTypes.Where(d => d.SelectedItems != null && d.SupportsSelectiveCleanup).ToList();

            // Confirm
            string confirmMsg = $"This will copy {selectedTypes.Count} .MAP file(s) from:\n" +
                               $"  {_selectedSourceProfile.Name}\n" +
                               $"  ({_selectedSourceProfile.DatabasePath})\n\n" +
                               $"To the current profile's DATABASE folder:\n" +
                               $"  {_currentProfileDatabasePath}\n\n" +
                               (chkProfileBackup.IsChecked == true ? "A backup will be created first.\n\n" : "WARNING: No backup will be created!\n\n") +
                               (selectiveTypes.Count > 0 ? $"{selectiveTypes.Count} data type(s) have selective items - cleanup will run after restart.\n\n" : "") +
                               "AutoCAD must be restarted after copying.\n\n" +
                               "Continue?";

            if (MessageBox.Show(confirmMsg, "Confirm Copy", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _isProfileCopying = true;
            SetProfileUIEnabled(false);
            pnlProfileProgress.Visibility = Visibility.Visible;

            var options = new MergeOptions
            {
                CreateBackup = chkProfileBackup.IsChecked == true,
                SelectedDataTypes = selectedTypes
            };

            _profileCopyService.ProgressChanged += ProfileCopyService_ProgressChanged;

            try
            {
                var result = _profileCopyService.CopyData(_selectedSourceProfile, _currentProfileDatabasePath, options);

                if (result.Success && selectiveTypes.Count > 0)
                {
                    // Build pending cleanup: items to DELETE = manifest items NOT in SelectedItems
                    SavePendingCleanup(selectiveTypes);
                }

                string summary = result.GetSummary();
                if (result.Success && selectiveTypes.Count > 0)
                    summary += $"\n\n{selectiveTypes.Count} data type(s) have selective items - cleanup will run after restart.";

                MessageBox.Show(
                    summary,
                    result.Success ? "Copy Complete" : "Copy Failed",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (result.Success)
                {
                    txtProfileStatus.Text = $"Last copy: {result.CopiedFiles.Count} files from {_selectedSourceProfile.Name}. Restart required.";
                    RefreshBackupList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error during copy:\n\n{ex.Message}",
                    "Copy Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _profileCopyService.ProgressChanged -= ProfileCopyService_ProgressChanged;
                _isProfileCopying = false;
                SetProfileUIEnabled(true);
            }
        }

        private void SavePendingCleanup(List<DataTypeDescriptor> selectiveTypes)
        {
            try
            {
                var manifest = _manifestService.LoadManifest(_selectedSourceProfile.DatabasePath);
                if (manifest == null) return;

                var cleanup = new PendingCleanup
                {
                    ProfileName = _selectedSourceProfile.Name,
                    DatabasePath = _currentProfileDatabasePath,
                    CreatedAt = DateTime.Now
                };

                foreach (var dt in selectiveTypes)
                {
                    if (dt.SelectedItems == null || dt.ManifestKey == null) continue;
                    if (!manifest.DataTypes.ContainsKey(dt.ManifestKey)) continue;

                    var allItems = manifest.DataTypes[dt.ManifestKey];
                    var keepSet = new HashSet<string>(dt.SelectedItems);
                    var toDelete = allItems
                        .Where(i => !keepSet.Contains(i.Name))
                        .Select(i => i.Name)
                        .ToList();

                    if (toDelete.Count > 0)
                        cleanup.ItemsToDelete[dt.ManifestKey] = toDelete;
                }

                if (cleanup.ItemsToDelete.Count > 0)
                    _cleanupService.SavePendingCleanup(cleanup, _currentProfileDatabasePath);
            }
            catch { }
        }

        private void SavePendingCleanupForProfile(List<DataTypeDescriptor> selectiveTypes, ProfileInfo sourceProfile, string targetDatabasePath)
        {
            try
            {
                var manifest = _manifestService.LoadManifest(sourceProfile.DatabasePath);
                if (manifest == null) return;

                var cleanup = new PendingCleanup
                {
                    ProfileName = sourceProfile.Name,
                    DatabasePath = targetDatabasePath,
                    CreatedAt = DateTime.Now
                };

                foreach (var dt in selectiveTypes)
                {
                    if (dt.SelectedItems == null || dt.ManifestKey == null) continue;
                    if (!manifest.DataTypes.ContainsKey(dt.ManifestKey)) continue;

                    var allItems = manifest.DataTypes[dt.ManifestKey];
                    var keepSet = new HashSet<string>(dt.SelectedItems);
                    var toDelete = allItems
                        .Where(i => !keepSet.Contains(i.Name))
                        .Select(i => i.Name)
                        .ToList();

                    if (toDelete.Count > 0)
                        cleanup.ItemsToDelete[dt.ManifestKey] = toDelete;
                }

                if (cleanup.ItemsToDelete.Count > 0)
                    _cleanupService.SavePendingCleanup(cleanup, targetDatabasePath);
            }
            catch { }
        }

        private void ProfileCopyService_ProgressChanged(object sender, CopyProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                prgProfileCopy.Value = e.PercentComplete;
                txtProfileCopyStatus.Text = e.Message;
            });
        }

        private void SetProfileUIEnabled(bool enabled)
        {
            cmbProfileSource.IsEnabled = enabled;
            lstProfileDataTypes.IsEnabled = enabled;
            chkProfileBackup.IsEnabled = enabled;
            btnProfileSelectAll.IsEnabled = enabled;
            btnProfileSelectNone.IsEnabled = enabled;
            btnProfileSelectPriceLabor.IsEnabled = enabled;
            btnProfileCopy.IsEnabled = enabled;

            // Push controls
            btnPushToProfiles.IsEnabled = enabled;
            btnPushSelectAll.IsEnabled = enabled;
            btnPushSelectNone.IsEnabled = enabled;
            foreach (var cb in _pushTargetCheckBoxes)
                cb.IsEnabled = enabled;
        }

        // -- Push to Profiles --

        private void PopulatePushTargetProfiles(List<ProfileInfo> profiles)
        {
            pnlPushTargetProfiles.Children.Clear();
            _pushTargetCheckBoxes.Clear();

            // Add all named profiles (skip Global which is the current profile)
            foreach (var profile in profiles.Where(p => !p.IsCurrent && p.Name != "Global"))
            {
                var cb = new CheckBox
                {
                    Content = profile.ToString(),
                    Tag = profile,
                    Margin = new Thickness(0, 2, 0, 2),
                    IsChecked = false
                };
                _pushTargetCheckBoxes.Add(cb);
                pnlPushTargetProfiles.Children.Add(cb);
            }

            if (_pushTargetCheckBoxes.Count == 0)
            {
                pnlPushTargetProfiles.Children.Add(new TextBlock
                {
                    Text = "No named profiles found.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                btnPushToProfiles.IsEnabled = false;
            }
        }

        private void btnPushSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _pushTargetCheckBoxes)
                cb.IsChecked = true;
        }

        private void btnPushSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _pushTargetCheckBoxes)
                cb.IsChecked = false;
        }

        private void btnPushToProfiles_Click(object sender, RoutedEventArgs e)
        {
            if (_isPushInProgress || _isProfileCopying) return;

            // Validate data types selected
            var selectedTypes = _profileDataTypes?.Where(d => d.IsSelected && d.IsAvailable).ToList();
            if (selectedTypes == null || selectedTypes.Count == 0)
            {
                MessageBox.Show("Please select at least one data type to push.", "No Data Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate target profiles
            var targetProfiles = _pushTargetCheckBoxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => cb.Tag as ProfileInfo)
                .Where(p => p != null)
                .ToList();

            if (targetProfiles.Count == 0)
            {
                MessageBox.Show("Please select at least one target profile.", "No Targets Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build the Global source ProfileInfo
            string dbRoot = ProfileCopy.Utilities.ProfilePathHelper.GetDatabaseRoot(_currentProfileDatabasePath);
            var globalSource = new ProfileInfo
            {
                Name = "Global",
                DatabasePath = _currentProfileDatabasePath,
                Path = dbRoot
            };

            if (!globalSource.IsValid())
            {
                MessageBox.Show("Global DATABASE folder not found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Check how many data types have selective items
            var selectiveTypes = selectedTypes.Where(d => d.SelectedItems != null && d.SupportsSelectiveCleanup).ToList();

            // Confirmation
            string typeNames = string.Join(", ", selectedTypes.Select(d => d.DisplayName));
            string profileNames = string.Join("\n  ", targetProfiles.Select(p => p.Name));
            string confirmMsg = $"Push {selectedTypes.Count} data type(s) from Global to {targetProfiles.Count} profile(s):\n\n" +
                               $"Data: {typeNames}\n\n" +
                               $"Targets:\n  {profileNames}\n\n" +
                               (chkProfileBackup.IsChecked == true ? "A backup will be created for each target profile.\n\n" : "WARNING: No backups will be created!\n\n") +
                               (selectiveTypes.Count > 0 ? $"{selectiveTypes.Count} data type(s) have selective items - cleanup will run when each profile is loaded.\n\n" : "") +
                               "AutoCAD must be restarted after pushing.\n\n" +
                               "Continue?";

            if (MessageBox.Show(confirmMsg, "Confirm Push to Profiles",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _isPushInProgress = true;
            SetProfileUIEnabled(false);
            pnlPushProgress.Visibility = Visibility.Visible;
            txtPushResultStatus.Text = "";

            var results = new List<PushProfileResult>();

            try
            {
                for (int i = 0; i < targetProfiles.Count; i++)
                {
                    var target = targetProfiles[i];
                    string progressMsg = $"Pushing to {target.Name} ({i + 1}/{targetProfiles.Count})...";

                    Dispatcher.Invoke(() =>
                    {
                        prgPushProgress.Value = (double)i / targetProfiles.Count * 100;
                        txtPushStatus.Text = progressMsg;
                    });

                    var options = new MergeOptions
                    {
                        CreateBackup = chkProfileBackup.IsChecked == true,
                        SelectedDataTypes = selectedTypes
                    };

                    var copyResult = _profileCopyService.CopyData(globalSource, target.DatabasePath, options);

                    if (copyResult.Success && selectiveTypes.Count > 0)
                    {
                        SavePendingCleanupForProfile(selectiveTypes, globalSource, target.DatabasePath);
                    }

                    results.Add(new PushProfileResult
                    {
                        ProfileName = target.Name,
                        Result = copyResult
                    });
                }

                // Show summary
                Dispatcher.Invoke(() =>
                {
                    prgPushProgress.Value = 100;
                    txtPushStatus.Text = "Push complete.";
                });

                ShowPushSummary(results);

                int successCount = results.Count(r => r.Result.Success);
                txtPushResultStatus.Text = $"Push complete: {successCount}/{results.Count} profiles updated successfully.";

                if (successCount > 0)
                    RefreshBackupList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error during push:\n\n{ex.Message}",
                    "Push Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isPushInProgress = false;
                SetProfileUIEnabled(true);
                pnlPushProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPushSummary(List<PushProfileResult> results)
        {
            var sb = new StringBuilder();
            int successCount = results.Count(r => r.Result.Success);
            int failCount = results.Count - successCount;

            sb.AppendLine($"Push to Profiles Complete");
            sb.AppendLine($"========================");
            sb.AppendLine($"Succeeded: {successCount}  |  Failed: {failCount}");
            sb.AppendLine();

            foreach (var r in results)
            {
                if (r.Result.Success)
                {
                    sb.AppendLine($"  {r.ProfileName}: {r.Result.CopiedFiles.Count} file(s) copied");
                }
                else
                {
                    sb.AppendLine($"  {r.ProfileName}: FAILED - {r.Result.ErrorMessage}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("IMPORTANT: You must restart AutoCAD for changes to take effect.");

            MessageBox.Show(sb.ToString(),
                failCount == 0 ? "Push Complete" : "Push Complete (with errors)",
                MessageBoxButton.OK,
                failCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private class PushProfileResult
        {
            public string ProfileName { get; set; }
            public CopyResult Result { get; set; }
        }

        // -- Backup / Restore --

        private void RefreshBackupList()
        {
            try
            {
                string backupDir = ProfilePathHelper.GetBackupDirectory();
                if (!Directory.Exists(backupDir))
                {
                    cmbProfileBackups.ItemsSource = null;
                    txtProfileBackupInfo.Text = "No backups found.";
                    btnProfileRestore.IsEnabled = false;
                    return;
                }

                var backups = Directory.GetFiles(backupDir, "Backup_*.zip")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (backups.Count == 0)
                {
                    cmbProfileBackups.ItemsSource = null;
                    txtProfileBackupInfo.Text = "No backups found.";
                    btnProfileRestore.IsEnabled = false;
                    return;
                }

                // Display as friendly name + date
                var items = backups.Select(f => new BackupItem
                {
                    FilePath = f.FullName,
                    DisplayName = $"{Path.GetFileNameWithoutExtension(f.Name)}  ({f.CreationTime:g}, {f.Length / 1024:N0} KB)"
                }).ToList();

                cmbProfileBackups.ItemsSource = items;
                cmbProfileBackups.DisplayMemberPath = "DisplayName";
                txtProfileBackupInfo.Text = $"{backups.Count} backup(s) available.";
                btnProfileRestore.IsEnabled = false; // enabled when one is selected
            }
            catch (Exception ex)
            {
                txtProfileBackupInfo.Text = $"Error loading backups: {ex.Message}";
            }
        }

        private void cmbProfileBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = cmbProfileBackups.SelectedItem as BackupItem;
            btnProfileRestore.IsEnabled = selected != null;

            if (selected != null)
            {
                try
                {
                    var fi = new FileInfo(selected.FilePath);
                    txtProfileBackupInfo.Text = $"Created: {fi.CreationTime:yyyy-MM-dd HH:mm:ss}  |  Size: {fi.Length / 1024:N0} KB";
                }
                catch
                {
                    txtProfileBackupInfo.Text = selected.FilePath;
                }
            }
        }

        private void btnProfileRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_isProfileCopying) return;

            var selected = cmbProfileBackups.SelectedItem as BackupItem;
            if (selected == null || !File.Exists(selected.FilePath))
            {
                MessageBox.Show("Please select a backup to restore.", "No Backup Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_currentProfileDatabasePath))
            {
                MessageBox.Show("No active profile detected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string confirmMsg = $"This will restore the backup:\n" +
                               $"  {Path.GetFileName(selected.FilePath)}\n\n" +
                               $"To the current profile's DATABASE folder:\n" +
                               $"  {_currentProfileDatabasePath}\n\n" +
                               "Existing .MAP files will be overwritten with the backup contents.\n" +
                               "AutoCAD must be restarted after restoring.\n\n" +
                               "Continue?";

            if (MessageBox.Show(confirmMsg, "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                _backupService.RestoreBackup(selected.FilePath, _currentProfileDatabasePath);

                MessageBox.Show(
                    "Backup restored successfully.\n\nPlease restart AutoCAD for changes to take effect.",
                    "Restore Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                txtProfileStatus.Text = "Backup restored. Restart required.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error restoring backup:\n\n{ex.Message}",
                    "Restore Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void btnProfileOpenBackups_Click(object sender, RoutedEventArgs e)
        {
            string backupDir = ProfilePathHelper.GetBackupDirectory();
            ProfilePathHelper.EnsureDirectoryExists(backupDir);

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", backupDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open folder:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Simple wrapper for backup file info displayed in the ComboBox.
        /// </summary>
        private class BackupItem
        {
            public string FilePath { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
