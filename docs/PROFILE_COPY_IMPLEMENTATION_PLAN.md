# Profile Data Copy - Implementation Plan

## Quick Reference Guide

This document provides step-by-step implementation instructions with code templates for the Profile Data Copy feature.

---

## PHASE 1: Core Infrastructure

### Step 1.1: Create Models

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Models\ProfileCopy\ProfileInfo.cs`

```csharp
using System;
using System.IO;

namespace FabricationSample.Models.ProfileCopy
{
    /// <summary>
    /// Represents a Fabrication profile on the system.
    /// </summary>
    public class ProfileInfo
    {
        /// <summary>Profile display name</summary>
        public string Name { get; set; }

        /// <summary>Full path to profile directory</summary>
        public string Path { get; set; }

        /// <summary>Full path to DATABASE folder</summary>
        public string DatabasePath { get; set; }

        /// <summary>Fabrication version (e.g., "2024", "2025")</summary>
        public string Version { get; set; }

        /// <summary>Whether this is the currently loaded profile</summary>
        public bool IsCurrent { get; set; }

        /// <summary>Display string for UI</summary>
        public override string ToString() => $"{Name} (Fabrication {Version})";

        /// <summary>Validate that profile directory exists</summary>
        public bool IsValid()
        {
            return Directory.Exists(Path) &&
                   Directory.Exists(DatabasePath);
        }
    }

    /// <summary>
    /// Counts of data types available in a profile.
    /// </summary>
    public class DataTypeCounts
    {
        public bool HasServices { get; set; }
        public bool HasPriceLists { get; set; }
        public bool HasInstallTimes { get; set; }
        public bool HasMaterials { get; set; }
        public bool HasGauges { get; set; }
        public bool HasSpecifications { get; set; }
        public bool HasCustomData { get; set; }
        public bool HasServiceTemplates { get; set; }
    }
}
```

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Models\ProfileCopy\DataTypeDescriptor.cs`

```csharp
using System.ComponentModel;

namespace FabricationSample.Models.ProfileCopy
{
    /// <summary>
    /// Enumeration of copyable data types.
    /// </summary>
    public enum DataType
    {
        Services,
        PriceLists,
        InstallationTimes,
        FabricationTimes,
        Materials,
        Gauges,
        Specifications,
        CustomData,
        ServiceTemplates,
        Sections
    }

    /// <summary>
    /// Describes a data type available for copying.
    /// Implements INotifyPropertyChanged for UI binding.
    /// </summary>
    public class DataTypeDescriptor : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _itemCount;

        public DataType Type { get; set; }
        public string DisplayName { get; set; }
        public string MapFileName { get; set; }
        public string Description { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public int ItemCount
        {
            get => _itemCount;
            set
            {
                if (_itemCount != value)
                {
                    _itemCount = value;
                    OnPropertyChanged(nameof(ItemCount));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        /// <summary>Display text with count</summary>
        public string DisplayText => ItemCount > 0
            ? $"{DisplayName} ({ItemCount} items)"
            : DisplayName;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>Factory method to create all supported data types</summary>
        public static DataTypeDescriptor[] GetSupportedTypes()
        {
            return new[]
            {
                new DataTypeDescriptor
                {
                    Type = DataType.Services,
                    DisplayName = "Services",
                    MapFileName = "service.map",
                    Description = "Service definitions with specifications and templates"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.PriceLists,
                    DisplayName = "Price Lists",
                    MapFileName = "costs.map",
                    Description = "Supplier groups and price lists"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.InstallationTimes,
                    DisplayName = "Installation Times",
                    MapFileName = "install.map",
                    Description = "Installation time tables and rates"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.Materials,
                    DisplayName = "Materials",
                    MapFileName = "material.map",
                    Description = "Material definitions"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.Gauges,
                    DisplayName = "Gauges",
                    MapFileName = "gauge.map",
                    Description = "Gauge and thickness definitions"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.Specifications,
                    DisplayName = "Specifications",
                    MapFileName = "spec.map",
                    Description = "Specification groups"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.ServiceTemplates,
                    DisplayName = "Service Templates",
                    MapFileName = "srvtempl.map",
                    Description = "Service template definitions"
                },
                new DataTypeDescriptor
                {
                    Type = DataType.CustomData,
                    DisplayName = "Custom Data",
                    MapFileName = "custdata.map",
                    Description = "Custom data field definitions"
                }
            };
        }
    }
}
```

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Models\ProfileCopy\MergeStrategy.cs`

```csharp
namespace FabricationSample.Models.ProfileCopy
{
    /// <summary>
    /// Strategy for handling duplicate items during import.
    /// </summary>
    public enum MergeStrategy
    {
        /// <summary>Add new items only, skip items that already exist</summary>
        SkipDuplicates,

        /// <summary>Add new items, update existing items with new data</summary>
        UpdateExisting,

        /// <summary>Delete all existing items and import fresh</summary>
        ReplaceAll,

        /// <summary>Add all items as new (may create duplicates)</summary>
        AppendAll
    }

    /// <summary>
    /// Configuration options for merge operation.
    /// </summary>
    public class MergeOptions
    {
        public MergeStrategy Strategy { get; set; } = MergeStrategy.SkipDuplicates;

        /// <summary>Create backup before making changes</summary>
        public bool CreateBackup { get; set; } = true;

        /// <summary>Validate imported data before applying</summary>
        public bool ValidateBeforeImport { get; set; } = true;

        /// <summary>Stop import on first error vs continue</summary>
        public bool StopOnFirstError { get; set; } = false;

        /// <summary>Show detailed progress messages</summary>
        public bool VerboseProgress { get; set; } = false;
    }
}
```

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Models\ProfileCopy\CopyResult.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace FabricationSample.Models.ProfileCopy
{
    /// <summary>
    /// Result of a profile data copy operation.
    /// </summary>
    public class CopyResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<DataType, ImportResult> TypeResults { get; set; }
            = new Dictionary<DataType, ImportResult>();
        public TimeSpan Duration { get; set; }
        public string BackupPath { get; set; }
        public DateTime CompletedAt { get; set; }

        // Aggregate counts
        public int TotalAdded => TypeResults.Values.Sum(r => r.AddedCount);
        public int TotalUpdated => TypeResults.Values.Sum(r => r.UpdatedCount);
        public int TotalSkipped => TypeResults.Values.Sum(r => r.SkippedCount);
        public int TotalErrors => TypeResults.Values.Sum(r => r.Errors.Count);

        public void AddTypeResult(DataType type, ImportResult result)
        {
            TypeResults[type] = result;
        }

        public static CopyResult Cancelled()
        {
            return new CopyResult
            {
                Success = false,
                ErrorMessage = "Operation was cancelled by user",
                CompletedAt = DateTime.Now
            };
        }

        /// <summary>Generate summary text for display</summary>
        public string GetSummary()
        {
            if (!Success)
                return $"Import failed: {ErrorMessage}";

            var summary = $"Import completed successfully in {Duration.TotalSeconds:F1} seconds.\n\n";
            summary += $"Added: {TotalAdded}\n";
            summary += $"Updated: {TotalUpdated}\n";
            summary += $"Skipped: {TotalSkipped}\n";

            if (TotalErrors > 0)
                summary += $"Errors: {TotalErrors}\n";

            if (!string.IsNullOrEmpty(BackupPath))
                summary += $"\nBackup created at:\n{BackupPath}";

            return summary;
        }
    }

    /// <summary>
    /// Result of importing a specific data type.
    /// </summary>
    public class ImportResult
    {
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ReplacedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Progress update during copy operation.
    /// </summary>
    public class CopyProgress
    {
        public string Message { get; set; }
        public int Percentage { get; set; }
        public DataType? CurrentDataType { get; set; }
    }

    /// <summary>
    /// Temporary storage for exported data between profiles.
    /// </summary>
    public class ExportedDataSet
    {
        public string TempFolder { get; set; }
        public string ServicesPath { get; set; }
        public string PriceListsPath { get; set; }
        public string MaterialsPath { get; set; }
        public string GaugesPath { get; set; }
        public string SpecificationsPath { get; set; }
        public string InstallationTimesPath { get; set; }

        /// <summary>Clean up temporary files</summary>
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(TempFolder))
                    Directory.Delete(TempFolder, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
```

### Step 1.2: Create Utilities

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Utilities\ProfilePathHelper.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// Helper class for Fabrication profile path operations.
    /// </summary>
    public static class ProfilePathHelper
    {
        /// <summary>
        /// Gets common Fabrication installation base paths.
        /// </summary>
        public static IEnumerable<string> GetFabricationBasePaths()
        {
            var basePath = @"C:\ProgramData\Autodesk\Fabrication";

            // Check for versions 2024, 2025, 2026
            for (int year = 2024; year <= 2026; year++)
            {
                var versionPath = Path.Combine(basePath, year.ToString());
                if (Directory.Exists(versionPath))
                    yield return versionPath;
            }

            // Also check for version-less path
            if (Directory.Exists(basePath))
            {
                var dirs = Directory.GetDirectories(basePath)
                    .Where(d => !d.Contains("2024") &&
                                !d.Contains("2025") &&
                                !d.Contains("2026"));
                foreach (var dir in dirs)
                    yield return dir;
            }
        }

        /// <summary>
        /// Validates that a path is a valid Fabrication profile.
        /// </summary>
        public static bool IsValidProfilePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            var databasePath = Path.Combine(path, "DATABASE");
            return Directory.Exists(databasePath);
        }

        /// <summary>
        /// Gets the database path for a profile.
        /// </summary>
        public static string GetDatabasePath(string profilePath)
        {
            return Path.Combine(profilePath, "DATABASE");
        }

        /// <summary>
        /// Checks if a specific .map file exists in profile.
        /// </summary>
        public static bool HasMapFile(string profilePath, string mapFileName)
        {
            var databasePath = GetDatabasePath(profilePath);
            var mapPath = Path.Combine(databasePath, mapFileName);
            return File.Exists(mapPath);
        }

        /// <summary>
        /// Get Fabrication version from path.
        /// </summary>
        public static string GetVersionFromPath(string path)
        {
            // Extract year from path like "C:\...\Fabrication\2024\Profile"
            var parts = path.Split(Path.DirectorySeparatorChar);
            var fabricationIndex = Array.FindIndex(parts,
                p => p.Equals("Fabrication", StringComparison.OrdinalIgnoreCase));

            if (fabricationIndex >= 0 && fabricationIndex < parts.Length - 1)
            {
                var nextPart = parts[fabricationIndex + 1];
                if (int.TryParse(nextPart, out int year) && year >= 2020 && year <= 2030)
                    return year.ToString();
            }

            return "Unknown";
        }
    }
}
```

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Utilities\BackupHelper.cs`

```csharp
using System;
using System.IO;
using System.IO.Compression;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// Helper class for creating and restoring database backups.
    /// </summary>
    public static class BackupHelper
    {
        private static readonly string BackupRootPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FabricationSample",
                "Backups");

        /// <summary>
        /// Creates a backup of the specified database folder.
        /// </summary>
        /// <param name="databasePath">Path to DATABASE folder</param>
        /// <returns>Path to backup ZIP file</returns>
        public static string CreateBackup(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath) || !Directory.Exists(databasePath))
                throw new ArgumentException("Invalid database path", nameof(databasePath));

            // Ensure backup directory exists
            if (!Directory.Exists(BackupRootPath))
                Directory.CreateDirectory(BackupRootPath);

            // Generate backup filename with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var profileName = Path.GetFileName(Path.GetDirectoryName(databasePath));
            var backupFileName = $"Backup_{profileName}_{timestamp}.zip";
            var backupPath = Path.Combine(BackupRootPath, backupFileName);

            // Create ZIP archive of database folder
            ZipFile.CreateFromDirectory(databasePath, backupPath, CompressionLevel.Fastest, false);

            return backupPath;
        }

        /// <summary>
        /// Restores a backup to the specified location.
        /// </summary>
        /// <param name="backupPath">Path to backup ZIP file</param>
        /// <param name="targetDatabasePath">Target DATABASE folder path</param>
        public static void RestoreBackup(string backupPath, string targetDatabasePath)
        {
            if (!File.Exists(backupPath))
                throw new FileNotFoundException("Backup file not found", backupPath);

            if (!Directory.Exists(targetDatabasePath))
                throw new DirectoryNotFoundException("Target database path not found");

            // Extract backup to temp location first
            var tempRestorePath = Path.Combine(Path.GetTempPath(), $"FabRestore_{Guid.NewGuid()}");
            ZipFile.ExtractToDirectory(backupPath, tempRestorePath);

            try
            {
                // Copy files from temp to target
                foreach (var file in Directory.GetFiles(tempRestorePath))
                {
                    var fileName = Path.GetFileName(file);
                    var targetFile = Path.Combine(targetDatabasePath, fileName);

                    // Backup existing file before overwriting
                    if (File.Exists(targetFile))
                    {
                        File.Copy(targetFile, targetFile + ".old", true);
                    }

                    File.Copy(file, targetFile, true);
                }
            }
            finally
            {
                // Clean up temp restore path
                if (Directory.Exists(tempRestorePath))
                    Directory.Delete(tempRestorePath, true);
            }
        }

        /// <summary>
        /// Deletes old backups, keeping only the most recent N backups.
        /// </summary>
        /// <param name="keepCount">Number of backups to keep</param>
        public static void CleanOldBackups(int keepCount = 10)
        {
            if (!Directory.Exists(BackupRootPath))
                return;

            var backups = Directory.GetFiles(BackupRootPath, "*.zip")
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            // Delete backups beyond keepCount
            for (int i = keepCount; i < backups.Count; i++)
            {
                try
                {
                    File.Delete(backups[i]);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }

        /// <summary>
        /// Gets the size of a backup file in MB.
        /// </summary>
        public static double GetBackupSizeMB(string backupPath)
        {
            if (!File.Exists(backupPath))
                return 0;

            var fileInfo = new FileInfo(backupPath);
            return fileInfo.Length / (1024.0 * 1024.0);
        }
    }
}
```

### Step 1.3: Create Profile Discovery Service

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Services\ProfileCopy\ProfileDiscoveryService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FabricationSample.Models.ProfileCopy;
using FabricationSample.Utilities;
using FabApp = Autodesk.Fabrication.ApplicationServices.Application;

namespace FabricationSample.Services.ProfileCopy
{
    /// <summary>
    /// Service for discovering available Fabrication profiles.
    /// </summary>
    public class ProfileDiscoveryService
    {
        /// <summary>
        /// Gets all available Fabrication profiles on the system.
        /// Excludes the currently loaded profile.
        /// </summary>
        public List<ProfileInfo> GetAvailableProfiles()
        {
            var profiles = new List<ProfileInfo>();
            var currentProfile = GetCurrentProfileName();

            foreach (var basePath in ProfilePathHelper.GetFabricationBasePaths())
            {
                try
                {
                    var profileDirs = Directory.GetDirectories(basePath);

                    foreach (var profilePath in profileDirs)
                    {
                        if (!ProfilePathHelper.IsValidProfilePath(profilePath))
                            continue;

                        var profileName = Path.GetFileName(profilePath);

                        // Skip current profile
                        if (profileName.Equals(currentProfile, StringComparison.OrdinalIgnoreCase))
                            continue;

                        profiles.Add(new ProfileInfo
                        {
                            Name = profileName,
                            Path = profilePath,
                            DatabasePath = ProfilePathHelper.GetDatabasePath(profilePath),
                            Version = ProfilePathHelper.GetVersionFromPath(profilePath),
                            IsCurrent = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue searching other paths
                    System.Diagnostics.Debug.WriteLine($"Error scanning {basePath}: {ex.Message}");
                }
            }

            return profiles.OrderBy(p => p.Version)
                          .ThenBy(p => p.Name)
                          .ToList();
        }

        /// <summary>
        /// Gets information about the currently loaded profile.
        /// </summary>
        public ProfileInfo GetCurrentProfile()
        {
            try
            {
                var currentName = FabApp.CurrentProfile;
                var databasePath = FabApp.DatabasePath;
                var profilePath = Path.GetDirectoryName(databasePath);

                return new ProfileInfo
                {
                    Name = currentName,
                    Path = profilePath,
                    DatabasePath = databasePath,
                    Version = ProfilePathHelper.GetVersionFromPath(profilePath),
                    IsCurrent = true
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks which data types are available in a profile.
        /// </summary>
        public DataTypeCounts GetDataTypeCounts(ProfileInfo profile)
        {
            if (profile == null || !profile.IsValid())
                return new DataTypeCounts();

            return new DataTypeCounts
            {
                HasServices = ProfilePathHelper.HasMapFile(profile.Path, "service.map"),
                HasPriceLists = ProfilePathHelper.HasMapFile(profile.Path, "costs.map"),
                HasInstallTimes = ProfilePathHelper.HasMapFile(profile.Path, "install.map"),
                HasMaterials = ProfilePathHelper.HasMapFile(profile.Path, "material.map"),
                HasGauges = ProfilePathHelper.HasMapFile(profile.Path, "gauge.map"),
                HasSpecifications = ProfilePathHelper.HasMapFile(profile.Path, "spec.map"),
                HasCustomData = ProfilePathHelper.HasMapFile(profile.Path, "custdata.map"),
                HasServiceTemplates = ProfilePathHelper.HasMapFile(profile.Path, "srvtempl.map")
            };
        }

        private string GetCurrentProfileName()
        {
            try
            {
                return FabApp.CurrentProfile ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
```

---

## PHASE 2: UI Development

### Step 2.1: Create Main Window

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Windows\ProfileDataCopyWindow.xaml`

```xml
<Window x:Class="FabricationSample.Windows.ProfileDataCopyWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Copy Data from Another Profile"
        Height="650" Width="600"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        Background="White"
        Loaded="Window_Loaded">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,20">
            <TextBlock Text="Import Database Configuration from Another Profile"
                       FontSize="16" FontWeight="Bold"
                       Margin="0,0,0,5"/>
            <TextBlock Text="Select a source profile and choose which data types to import."
                       Foreground="Gray"
                       TextWrapping="Wrap"/>
        </StackPanel>

        <!-- Source Profile Selection -->
        <GroupBox Grid.Row="1" Header="Source Profile" Padding="10" Margin="0,0,0,15">
            <StackPanel>
                <ComboBox x:Name="cmbSourceProfile"
                          Height="30"
                          SelectionChanged="cmbSourceProfile_SelectionChanged"
                          DisplayMemberPath="ToString"/>
                <TextBlock x:Name="txtProfilePath"
                           Margin="0,5,0,0"
                           Foreground="Gray"
                           FontSize="11"
                           TextWrapping="Wrap"/>
            </StackPanel>
        </GroupBox>

        <!-- Data Types Selection -->
        <GroupBox Grid.Row="2" Header="Data Types to Import" Padding="10" Margin="0,0,0,15">
            <StackPanel>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                    <Button x:Name="btnSelectAll" Content="Select All" Width="80"
                            Click="btnSelectAll_Click" Margin="0,0,5,0"/>
                    <Button x:Name="btnSelectNone" Content="Select None" Width="80"
                            Click="btnSelectNone_Click"/>
                </StackPanel>

                <ScrollViewer MaxHeight="200" VerticalScrollBarVisibility="Auto">
                    <ItemsControl x:Name="icDataTypes">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Margin="0,3">
                                    <CheckBox IsChecked="{Binding IsSelected, Mode=TwoWay}"
                                              Content="{Binding DisplayText}"
                                              ToolTip="{Binding Description}"/>
                                </StackPanel>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ScrollViewer>
            </StackPanel>
        </GroupBox>

        <!-- Merge Strategy -->
        <GroupBox Grid.Row="3" Header="Import Options" Padding="10" Margin="0,0,0,15">
            <StackPanel>
                <TextBlock Text="How should duplicates be handled?" Margin="0,0,0,5"/>

                <RadioButton x:Name="rbSkipDuplicates"
                             Content="Skip Duplicates (recommended)"
                             ToolTip="Add new items only, skip items that already exist"
                             IsChecked="True"
                             Margin="0,3"/>

                <RadioButton x:Name="rbUpdateExisting"
                             Content="Update Existing"
                             ToolTip="Add new items and update existing items with new data"
                             Margin="0,3"/>

                <RadioButton x:Name="rbReplaceAll"
                             Content="Replace All"
                             ToolTip="Delete all existing items and import fresh"
                             Margin="0,3"/>

                <Separator Margin="0,10"/>

                <CheckBox x:Name="chkCreateBackup"
                          Content="Create backup before import"
                          IsChecked="True"
                          Margin="0,3"/>

                <CheckBox x:Name="chkValidate"
                          Content="Validate data before importing"
                          IsChecked="True"
                          Margin="0,3"/>
            </StackPanel>
        </GroupBox>

        <!-- Progress -->
        <GroupBox Grid.Row="4" Header="Progress" Padding="10" Margin="0,0,0,15"
                  x:Name="grpProgress" Visibility="Collapsed">
            <StackPanel>
                <ProgressBar x:Name="prgImport" Height="25" Minimum="0" Maximum="100"/>
                <TextBlock x:Name="txtProgress"
                           Margin="0,5,0,0"
                           Text="Ready to import..."
                           HorizontalAlignment="Center"/>
            </StackPanel>
        </GroupBox>

        <!-- Buttons -->
        <StackPanel Grid.Row="5" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button x:Name="btnPreview" Content="Preview Changes" Width="120"
                    Height="30" Margin="0,0,10,0"
                    Click="btnPreview_Click"/>
            <Button x:Name="btnImport" Content="Start Import" Width="120"
                    Height="30" Margin="0,0,10,0"
                    Click="btnImport_Click"
                    IsEnabled="False"/>
            <Button x:Name="btnCancel" Content="Cancel" Width="80"
                    Height="30"
                    Click="btnCancel_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

**File**: `C:\Users\tphillips\source\repos\FabricationSample\Windows\ProfileDataCopyWindow.xaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FabricationSample.Models.ProfileCopy;
using FabricationSample.Services.ProfileCopy;

namespace FabricationSample.Windows
{
    public partial class ProfileDataCopyWindow : Window
    {
        private ProfileDiscoveryService _discoveryService;
        private List<DataTypeDescriptor> _dataTypes;
        private ProfileInfo _selectedProfile;

        public ProfileDataCopyWindow()
        {
            InitializeComponent();
            _discoveryService = new ProfileDiscoveryService();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAvailableProfiles();
            LoadDataTypes();
        }

        private void LoadAvailableProfiles()
        {
            try
            {
                var profiles = _discoveryService.GetAvailableProfiles();

                if (profiles.Count == 0)
                {
                    MessageBox.Show(
                        "No other Fabrication profiles found on this system.",
                        "No Profiles Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                    return;
                }

                cmbSourceProfile.ItemsSource = profiles;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading profiles: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadDataTypes()
        {
            _dataTypes = DataTypeDescriptor.GetSupportedTypes().ToList();
            icDataTypes.ItemsSource = _dataTypes;
        }

        private void cmbSourceProfile_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedProfile = cmbSourceProfile.SelectedItem as ProfileInfo;

            if (_selectedProfile != null)
            {
                txtProfilePath.Text = $"Path: {_selectedProfile.Path}";
                btnImport.IsEnabled = true;

                // Update data type counts
                UpdateDataTypeCounts();
            }
            else
            {
                txtProfilePath.Text = string.Empty;
                btnImport.IsEnabled = false;
            }
        }

        private void UpdateDataTypeCounts()
        {
            // In Phase 1, we can't get exact counts without loading profile
            // So we just check if .map files exist
            var counts = _discoveryService.GetDataTypeCounts(_selectedProfile);

            foreach (var dataType in _dataTypes)
            {
                switch (dataType.Type)
                {
                    case DataType.Services:
                        dataType.ItemCount = counts.HasServices ? -1 : 0;
                        break;
                    case DataType.PriceLists:
                        dataType.ItemCount = counts.HasPriceLists ? -1 : 0;
                        break;
                    case DataType.Materials:
                        dataType.ItemCount = counts.HasMaterials ? -1 : 0;
                        break;
                    // ... etc
                }
            }
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var dataType in _dataTypes)
                dataType.IsSelected = true;
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var dataType in _dataTypes)
                dataType.IsSelected = false;
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Preview functionality will be implemented in Phase 2.",
                "Preview",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            // Validate selection
            var selectedTypes = _dataTypes.Where(dt => dt.IsSelected).ToList();
            if (selectedTypes.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one data type to import.",
                    "No Data Types Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Confirm with user
            var confirmMsg = $"Import {selectedTypes.Count} data type(s) from profile '{_selectedProfile.Name}'?\n\n";
            confirmMsg += string.Join("\n", selectedTypes.Select(dt => $"  • {dt.DisplayName}"));

            if (MessageBox.Show(confirmMsg, "Confirm Import",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // Perform import
            await PerformImport(selectedTypes);
        }

        private async Task PerformImport(List<DataTypeDescriptor> selectedTypes)
        {
            // Show progress
            grpProgress.Visibility = Visibility.Visible;
            btnImport.IsEnabled = false;
            btnPreview.IsEnabled = false;

            try
            {
                // Get merge strategy
                MergeStrategy strategy = MergeStrategy.SkipDuplicates;
                if (rbUpdateExisting.IsChecked == true)
                    strategy = MergeStrategy.UpdateExisting;
                else if (rbReplaceAll.IsChecked == true)
                    strategy = MergeStrategy.ReplaceAll;

                var options = new MergeOptions
                {
                    Strategy = strategy,
                    CreateBackup = chkCreateBackup.IsChecked == true,
                    ValidateBeforeImport = chkValidate.IsChecked == true
                };

                // Create progress reporter
                var progress = new Progress<CopyProgress>(p =>
                {
                    prgImport.Value = p.Percentage;
                    txtProgress.Text = p.Message;
                });

                // Execute import
                var service = new ProfileDataCopyService();
                var result = await service.CopyDataAsync(
                    _selectedProfile,
                    selectedTypes,
                    options,
                    progress);

                // Show results
                ShowImportResults(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Import failed: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                grpProgress.Visibility = Visibility.Collapsed;
                btnImport.IsEnabled = true;
                btnPreview.IsEnabled = true;
            }
        }

        private void ShowImportResults(CopyResult result)
        {
            if (result.Success)
            {
                MessageBox.Show(
                    result.GetSummary(),
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(
                    result.GetSummary(),
                    "Import Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
```

---

## NEXT STEPS

1. Create the Models folder and add all model classes
2. Create the Utilities folder and add helper classes
3. Create the Services/ProfileCopy folder and add ProfileDiscoveryService
4. Create the Windows folder entry and add ProfileDataCopyWindow
5. Test the UI - it should show available profiles and data type selection
6. Implement ProfileDataCopyService (Phase 2)
7. Add integration with DatabaseEditor (button to launch window)

## Testing Checklist

- [ ] Models compile without errors
- [ ] ProfileDiscoveryService finds available profiles
- [ ] BackupHelper creates valid ZIP backups
- [ ] ProfileDataCopyWindow displays correctly
- [ ] Profile dropdown populates with discovered profiles
- [ ] Data type checkboxes work
- [ ] Select All / Select None buttons work
- [ ] Merge strategy radio buttons work

This completes Phase 1 infrastructure. Phase 2 will implement the actual data copy logic.
