using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Services;

namespace FabricationSample.ProfileCopy.Windows
{
    /// <summary>
    /// Profile Data Copy window - allows copying .MAP files from one Fabrication profile to the current one.
    /// Profiles are discovered from the ./profiles/ directory relative to the database root.
    /// </summary>
    public partial class ProfileDataCopyWindow : Window
    {
        private readonly ProfileDiscoveryService _discoveryService;
        private readonly ProfileCopyService _copyService;
        private readonly ProfileManifestService _manifestService;
        private readonly SelectiveCleanupService _cleanupService;
        private DataTypeDescriptor[] _dataTypes;
        private ProfileInfo _selectedSource;
        private string _currentDatabasePath;
        private bool _isCopying;

        public ProfileDataCopyWindow()
        {
            InitializeComponent();
            _discoveryService = new ProfileDiscoveryService();
            _copyService = new ProfileCopyService();
            _manifestService = new ProfileManifestService();
            _cleanupService = new SelectiveCleanupService();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check for pending selective cleanup from a previous copy
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
                    try { System.IO.File.Delete(_cleanupService.GetCleanupFilePath()); } catch { }
                }

                // Get current database path from Fabrication API
                _currentDatabasePath = _discoveryService.GetCurrentDatabasePath();

                if (string.IsNullOrEmpty(_currentDatabasePath))
                {
                    MessageBox.Show(
                        "Could not determine the current profile's database path.\n\n" +
                        "Please ensure Fabrication CADmep is loaded and a valid profile is active.",
                        "No Active Profile",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Close();
                    return;
                }

                // Resolve the actual destination path for copy operations
                string currentProfile = _discoveryService.GetCurrentProfileName();
                bool onGlobal = string.IsNullOrEmpty(currentProfile);
                string displayName = onGlobal ? "Global" : currentProfile;

                string dbRoot = FabricationSample.ProfileCopy.Utilities.ProfilePathHelper
                    .GetDatabaseRoot(_currentDatabasePath);
                if (!onGlobal && dbRoot != null)
                {
                    _currentDatabasePath = System.IO.Path.Combine(
                        dbRoot, "profiles", currentProfile, "DATABASE");
                }

                txtCurrentProfile.Text = $"Current: {displayName}  ({_currentDatabasePath})";

                // Generate manifest for current profile (fire-and-forget)
                try
                {
                    _manifestService.GenerateManifest(_currentDatabasePath, displayName);
                }
                catch { }

                // Discover all profiles (Global always first)
                var profiles = _discoveryService.GetAvailableProfiles(_currentDatabasePath);

                if (profiles.Count == 0)
                {
                    MessageBox.Show(
                        "No profiles were found in the database.",
                        "No Profiles Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                    return;
                }

                // Show all profiles - current is greyed out and unselectable
                cmbSourceProfile.ItemsSource = profiles;

                // Initialize data type descriptors with grouping
                _dataTypes = DataTypeDescriptor.GetAllDescriptors();
                lstDataTypes.ItemsSource = _dataTypes;

                // Set up group view
                var view = CollectionViewSource.GetDefaultView(_dataTypes);
                view.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error initializing profile discovery:\n\n{ex.Message}",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Close();
            }
        }

        private void cmbSourceProfile_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = cmbSourceProfile.SelectedItem as ProfileInfo;

            // Reject selection of the current profile
            if (selected != null && selected.IsCurrent)
            {
                cmbSourceProfile.SelectedItem = _selectedSource;
                return;
            }

            _selectedSource = selected;
            if (_selectedSource == null)
            {
                txtProfilePath.Text = "";
                return;
            }

            txtProfilePath.Text = _selectedSource.DatabasePath;

            // Check which .MAP files exist in the source profile
            _discoveryService.CheckAvailableDataTypes(_selectedSource, _dataTypes);

            // Load source manifest and update item counts
            UpdateManifestItemCounts();

            // Auto-select all available types and clear previous selective choices
            foreach (var dt in _dataTypes)
            {
                dt.IsSelected = dt.IsAvailable;
                dt.SelectedItems = null;
            }

            // Refresh the list to update checkbox states
            lstDataTypes.Items.Refresh();
            UpdateGroupCheckBoxStates();
        }

        private void UpdateManifestItemCounts()
        {
            if (_selectedSource == null || _dataTypes == null) return;

            var manifest = _manifestService.LoadManifest(_selectedSource.DatabasePath);

            foreach (var dt in _dataTypes)
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

        private void DataType_Click(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;

            var descriptor = textBlock.DataContext as DataTypeDescriptor;
            if (descriptor == null || !descriptor.IsEnumerable) return;

            if (_selectedSource == null)
            {
                MessageBox.Show("Please select a source profile first.", "No Source",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var manifest = _manifestService.LoadManifest(_selectedSource.DatabasePath);
            if (manifest == null || !manifest.DataTypes.ContainsKey(descriptor.ManifestKey))
            {
                MessageBox.Show(
                    $"No preview available for {descriptor.DisplayName}.\n\n" +
                    $"Load the \"{_selectedSource.Name}\" profile with this addin first to generate a manifest.",
                    "No Manifest", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var items = manifest.DataTypes[descriptor.ManifestKey];
            var preview = new DataTypePreviewWindow(
                descriptor.DisplayName,
                _selectedSource.Name,
                items,
                descriptor.SupportsSelectiveCleanup,
                descriptor.SelectedItems);

            preview.Owner = this;

            if (preview.ShowDialog() == true)
            {
                if (preview.SelectedItemNames.Count == items.Count)
                {
                    descriptor.SelectedItems = null;
                }
                else
                {
                    descriptor.SelectedItems = preview.SelectedItemNames;
                }
                descriptor.ManifestItemCount = items.Count;
                lstDataTypes.Items.Refresh();
            }
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTypes == null) return;
            foreach (var dt in _dataTypes.Where(d => d.IsAvailable))
                dt.IsSelected = true;
            lstDataTypes.Items.Refresh();
            UpdateGroupCheckBoxStates();
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTypes == null) return;
            foreach (var dt in _dataTypes)
                dt.IsSelected = false;
            lstDataTypes.Items.Refresh();
            UpdateGroupCheckBoxStates();
        }

        private void btnSelectPriceLabor_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTypes == null) return;
            // Clear all first, then select only Price & Labor group
            foreach (var dt in _dataTypes)
                dt.IsSelected = false;
            foreach (var dt in _dataTypes.Where(d => d.Group == "Price & Labor" && d.IsAvailable))
                dt.IsSelected = true;
            lstDataTypes.Items.Refresh();
            UpdateGroupCheckBoxStates();
        }

        private void GroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTypes == null) return;
            var cb = sender as CheckBox;
            if (cb == null) return;

            string groupName = cb.Tag as string;
            if (string.IsNullOrEmpty(groupName)) return;

            var groupItems = _dataTypes.Where(d => d.Group == groupName).ToList();
            bool allAvailableSelected = groupItems.Where(d => d.IsAvailable).All(d => d.IsSelected);

            if (allAvailableSelected)
            {
                foreach (var dt in groupItems)
                    dt.IsSelected = false;
                cb.IsChecked = false;
            }
            else
            {
                foreach (var dt in groupItems.Where(d => d.IsAvailable))
                    dt.IsSelected = true;
                cb.IsChecked = groupItems.Any(d => d.IsAvailable);
            }

            lstDataTypes.Items.Refresh();
        }

        private void UpdateGroupCheckBoxStates()
        {
            if (_dataTypes == null) return;

            var checkBoxes = FindVisualChildren<CheckBox>(lstDataTypes)
                .Where(cb => cb.Tag is string);

            foreach (var cb in checkBoxes)
            {
                string groupName = cb.Tag as string;
                if (string.IsNullOrEmpty(groupName)) continue;

                var availableItems = _dataTypes.Where(d => d.Group == groupName && d.IsAvailable).ToList();

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
                        cb.IsChecked = null;
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

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying) return;

            if (_selectedSource == null)
            {
                MessageBox.Show("Please select a source profile.", "No Source Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedTypes = _dataTypes?.Where(d => d.IsSelected && d.IsAvailable).ToList();
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
                               $"  {_selectedSource.Name}\n" +
                               $"  ({_selectedSource.DatabasePath})\n\n" +
                               $"To the current profile's DATABASE folder:\n" +
                               $"  {_currentDatabasePath}\n\n" +
                               (chkBackup.IsChecked == true ? "A backup will be created first.\n\n" : "WARNING: No backup will be created!\n\n") +
                               (selectiveTypes.Count > 0 ? $"{selectiveTypes.Count} data type(s) have selective items - cleanup will run after restart.\n\n" : "") +
                               "AutoCAD must be restarted after copying.\n\n" +
                               "Continue?";

            if (MessageBox.Show(confirmMsg, "Confirm Copy", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // Execute copy
            _isCopying = true;
            SetUIEnabled(false);
            pnlProgress.Visibility = Visibility.Visible;

            var options = new MergeOptions
            {
                CreateBackup = chkBackup.IsChecked == true,
                SelectedDataTypes = selectedTypes
            };

            _copyService.ProgressChanged += CopyService_ProgressChanged;

            try
            {
                var result = _copyService.CopyData(_selectedSource, _currentDatabasePath, options);

                if (result.Success && selectiveTypes.Count > 0)
                {
                    SavePendingCleanup(selectiveTypes);
                }

                string summary = result.GetSummary();
                if (result.Success && selectiveTypes.Count > 0)
                    summary += $"\n\n{selectiveTypes.Count} data type(s) have selective items - cleanup will run after restart.";

                // Show result
                MessageBox.Show(
                    summary,
                    result.Success ? "Copy Complete" : "Copy Failed",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (result.Success)
                {
                    DialogResult = true;
                    Close();
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
                _copyService.ProgressChanged -= CopyService_ProgressChanged;
                _isCopying = false;
                SetUIEnabled(true);
            }
        }

        private void SavePendingCleanup(List<DataTypeDescriptor> selectiveTypes)
        {
            try
            {
                var manifest = _manifestService.LoadManifest(_selectedSource.DatabasePath);
                if (manifest == null) return;

                var cleanup = new PendingCleanup
                {
                    ProfileName = _selectedSource.Name,
                    DatabasePath = _currentDatabasePath,
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
                    _cleanupService.SavePendingCleanup(cleanup);
            }
            catch { }
        }

        private void CopyService_ProgressChanged(object sender, CopyProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                prgCopy.Value = e.PercentComplete;
                txtStatus.Text = e.Message;
            });
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isCopying)
                return;

            DialogResult = false;
            Close();
        }

        private void SetUIEnabled(bool enabled)
        {
            cmbSourceProfile.IsEnabled = enabled;
            lstDataTypes.IsEnabled = enabled;
            chkBackup.IsEnabled = enabled;
            btnSelectAll.IsEnabled = enabled;
            btnSelectNone.IsEnabled = enabled;
            btnCopy.IsEnabled = enabled;
            btnCancel.IsEnabled = enabled;
        }
    }
}
