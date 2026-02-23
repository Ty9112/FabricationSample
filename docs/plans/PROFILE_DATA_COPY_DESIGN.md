# Profile Data Copy Feature - Design Document

## Executive Summary

This document outlines the design and implementation plan for a Profile Data Copy feature in FabricationSample. This feature will enable users to copy database configuration files (.map files) from one Fabrication profile to another, including services, price lists, installation times, materials, and other database elements.

---

## 1. FEATURE OVERVIEW

### 1.1 Purpose

Fabrication profiles contain configuration data stored in `.map` files within a `DATABASE` folder. Users frequently need to:
- Copy services from one profile to another
- Merge price lists between profiles
- Synchronize installation times across profiles
- Migrate materials, gauges, and specifications
- Bootstrap new profiles with existing data

### 1.2 Current Limitations

- No built-in UI for cross-profile data transfer
- Manual file copying risks data corruption
- No merge capability - only complete overwrites
- Difficult to selectively copy specific data types

### 1.3 Proposed Solution

A comprehensive UI-based tool that:
1. Allows selection of source profile (different from current)
2. Displays available data types with selection checkboxes
3. Provides merge strategies (append, overwrite, skip duplicates)
4. Validates data before import
5. Creates backup before modification
6. Provides detailed import summary

---

## 2. TECHNICAL BACKGROUND

### 2.1 Fabrication Profile Structure

```
C:\ProgramData\Autodesk\Fabrication\<VERSION>\<PROFILE_NAME>\
├── DATABASE\
│   ├── service.map          (Services)
│   ├── costs.map            (Price lists/Supplier groups)
│   ├── install.map          (Installation times)
│   ├── labrate.map          (Labor rates)
│   ├── material.map         (Materials)
│   ├── gauge.map            (Gauges)
│   ├── spec.map             (Specifications)
│   ├── custdata.map         (Custom data definitions)
│   ├── sections.map         (Section definitions)
│   ├── srvtempl.map         (Service templates)
│   └── [other .map files]
├── Content\                 (ITM files - not covered in this feature)
└── [other configuration files]
```

### 2.2 Fabrication API Access Points

```csharp
// Current profile information
string currentProfile = Autodesk.Fabrication.ApplicationServices.Application.CurrentProfile;
string databasePath = Autodesk.Fabrication.ApplicationServices.Application.DatabasePath;

// Current database collections (in-memory)
Database.Services              // Service collection
Database.SupplierGroups        // Price lists
Database.InstallationRates     // Installation time tables
Database.Materials             // Material collection
Database.Gauges                // Gauge collection
Database.Specifications        // Specification collection

// Save methods (write to current profile)
Database.SaveServices();
Database.SaveProductCosts();
Database.SaveInstallationTimes();
Database.SaveMaterials();
Database.SaveGauges();
Database.SaveSpecifications();
```

### 2.3 Key Challenges

1. **Multi-Profile Access**: Fabrication API only loads ONE profile at a time
2. **No Direct Load**: Cannot load from external profile while another is active
3. **Binary Format**: .map files are binary - cannot parse directly
4. **Relationship Integrity**: Services reference materials, gauges, specs
5. **ID Conflicts**: Database IDs may conflict between profiles

---

## 3. ARCHITECTURE DESIGN

### 3.1 Approach: Temporary Profile Switching

Since the Fabrication API can only work with one profile at a time, we must:
1. Save current job state
2. Switch to source profile temporarily
3. Export data to intermediate format (CSV or XML)
4. Switch back to target profile
5. Import from intermediate format

**Alternative Considered**: Direct .map file manipulation
- Rejected due to binary format complexity and risk of corruption
- Would require reverse-engineering proprietary format

### 3.2 Component Architecture

```
ProfileDataCopyFeature/
├── UI Layer
│   ├── ProfileDataCopyWindow.xaml/cs     (Main dialog)
│   ├── DataTypeSelectionPanel.xaml/cs    (Checkbox list of data types)
│   └── MergeOptionsPanel.xaml/cs         (Merge strategy options)
│
├── Service Layer
│   ├── ProfileDataCopyService.cs         (Orchestrator)
│   ├── ProfileDiscoveryService.cs        (Find available profiles)
│   ├── ProfileSwitchingService.cs        (Manage profile changes)
│   └── DataMergeService.cs               (Merge logic)
│
├── Models
│   ├── ProfileInfo.cs                    (Profile metadata)
│   ├── DataTypeDescriptor.cs             (Data type info)
│   ├── MergeStrategy.cs                  (Merge options)
│   └── CopyResult.cs                     (Operation results)
│
└── Utilities
    ├── ProfilePathHelper.cs              (Path resolution)
    └── BackupHelper.cs                   (Backup operations)
```

### 3.3 Data Flow Sequence

```
1. User opens Profile Data Copy dialog
   ↓
2. ProfileDiscoveryService scans for available profiles
   ↓
3. User selects source profile and data types
   ↓
4. User configures merge options
   ↓
5. ProfileDataCopyService orchestrates:
   a. Create backup of current profile
   b. Export current state to temp location
   c. Switch to source profile
   d. Export selected data types to intermediate format
   e. Switch back to target profile
   f. Restore from temp (if needed)
   g. Import data using merge strategy
   h. Save changes
   ↓
6. Display import summary to user
```

---

## 4. DETAILED COMPONENT SPECIFICATIONS

### 4.1 ProfileDataCopyWindow (Main UI)

**Purpose**: Primary user interface for profile data copying

**Layout**:
```
┌─ Profile Data Copy ─────────────────────────────────┐
│                                                      │
│  Source Profile                                      │
│  ┌──────────────────────────────────────────────┐  │
│  │ [Dropdown: Select source profile]            │  │
│  └──────────────────────────────────────────────┘  │
│  Path: C:\ProgramData\Autodesk\Fabrication\...     │
│                                                      │
│  ┌─ Data Types to Copy ─────────────────────────┐  │
│  │ ☑ Services (45 items)                        │  │
│  │ ☑ Price Lists (12 supplier groups)           │  │
│  │ ☑ Installation Times (8 tables)              │  │
│  │ ☑ Materials (156 items)                      │  │
│  │ ☑ Gauges (24 items)                          │  │
│  │ ☑ Specifications (18 items)                  │  │
│  │ ☐ Custom Data Definitions (5 items)          │  │
│  │ ☐ Service Templates (6 items)                │  │
│  │                                               │  │
│  │ [Select All] [Select None]                   │  │
│  └───────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Merge Strategy ──────────────────────────────┐  │
│  │ ○ Append (add new items only)                │  │
│  │ ● Skip Duplicates (by name)                  │  │
│  │ ○ Update Existing (overwrite by ID/name)     │  │
│  │ ○ Replace All (clear and import)             │  │
│  │                                               │  │
│  │ ☑ Create backup before import                │  │
│  │ ☑ Validate data before applying              │  │
│  └───────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Progress ────────────────────────────────────┐  │
│  │ ▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░ 50%                    │  │
│  │ Status: Importing services...                │  │
│  └───────────────────────────────────────────────┘  │
│                                                      │
│  [Preview Changes]    [Start Import]    [Cancel]    │
│                                                      │
└──────────────────────────────────────────────────────┘
```

**Key Methods**:
```csharp
public class ProfileDataCopyWindow : Window
{
    private void LoadAvailableProfiles()
    {
        // Discover all Fabrication profiles on system
        var profiles = ProfileDiscoveryService.GetAvailableProfiles();
        cmbSourceProfile.ItemsSource = profiles;
    }

    private void RefreshDataTypeCounts()
    {
        // Count items in selected source profile
        // Updates checkbox labels with counts
    }

    private void btnPreview_Click(object sender, RoutedEventArgs e)
    {
        // Show preview of what will be imported
        // Display conflicts and merge decisions
    }

    private async void btnStartImport_Click(object sender, RoutedEventArgs e)
    {
        // Orchestrate the import process
        var service = new ProfileDataCopyService();
        var result = await service.CopyDataAsync(
            sourceProfile,
            selectedDataTypes,
            mergeStrategy,
            progress);

        ShowImportSummary(result);
    }
}
```

---

### 4.2 ProfileDiscoveryService

**Purpose**: Locate and enumerate Fabrication profiles

```csharp
public class ProfileDiscoveryService
{
    /// <summary>
    /// Gets all available Fabrication profiles on the system.
    /// </summary>
    /// <returns>List of ProfileInfo objects</returns>
    public static List<ProfileInfo> GetAvailableProfiles()
    {
        var profiles = new List<ProfileInfo>();

        // Check common Fabrication installation locations
        var basePaths = new[]
        {
            @"C:\ProgramData\Autodesk\Fabrication\2024",
            @"C:\ProgramData\Autodesk\Fabrication\2025",
            @"C:\ProgramData\Autodesk\Fabrication\2026"
        };

        foreach (var basePath in basePaths)
        {
            if (Directory.Exists(basePath))
            {
                var profileDirs = Directory.GetDirectories(basePath);
                foreach (var dir in profileDirs)
                {
                    var databasePath = Path.Combine(dir, "DATABASE");
                    if (Directory.Exists(databasePath))
                    {
                        profiles.Add(new ProfileInfo
                        {
                            Name = Path.GetFileName(dir),
                            Path = dir,
                            DatabasePath = databasePath,
                            Version = Path.GetFileName(basePath)
                        });
                    }
                }
            }
        }

        // Exclude current profile
        string currentProfile =
            Autodesk.Fabrication.ApplicationServices.Application.CurrentProfile;
        profiles = profiles.Where(p => p.Name != currentProfile).ToList();

        return profiles;
    }

    /// <summary>
    /// Counts data elements in a profile without loading it.
    /// </summary>
    public static DataTypeCounts GetDataTypeCounts(ProfileInfo profile)
    {
        // Since we can't load multiple profiles, we estimate from file sizes
        // or return "Available" without counts
        // Real counts would require temporary profile switching

        return new DataTypeCounts
        {
            HasServices = File.Exists(Path.Combine(profile.DatabasePath, "service.map")),
            HasPriceLists = File.Exists(Path.Combine(profile.DatabasePath, "costs.map")),
            HasInstallTimes = File.Exists(Path.Combine(profile.DatabasePath, "install.map")),
            HasMaterials = File.Exists(Path.Combine(profile.DatabasePath, "material.map")),
            HasGauges = File.Exists(Path.Combine(profile.DatabasePath, "gauge.map")),
            HasSpecifications = File.Exists(Path.Combine(profile.DatabasePath, "spec.map"))
        };
    }
}
```

---

### 4.3 ProfileDataCopyService (Orchestrator)

**Purpose**: Coordinate the entire copy operation

```csharp
public class ProfileDataCopyService
{
    private IProgress<CopyProgress> _progress;
    private bool _isCancelled = false;

    /// <summary>
    /// Main entry point for copying data between profiles.
    /// </summary>
    public async Task<CopyResult> CopyDataAsync(
        ProfileInfo sourceProfile,
        List<DataTypeDescriptor> dataTypes,
        MergeStrategy strategy,
        IProgress<CopyProgress> progress)
    {
        _progress = progress;
        var result = new CopyResult();

        try
        {
            // Phase 1: Backup current profile
            ReportProgress("Creating backup...", 5);
            BackupHelper.CreateBackup(
                Autodesk.Fabrication.ApplicationServices.Application.DatabasePath);

            // Phase 2: Save current job state
            ReportProgress("Saving current job...", 10);
            string currentJobPath = SaveCurrentJobState();

            // Phase 3: Export from source profile
            ReportProgress("Switching to source profile...", 15);
            var exportedData = await ExportFromSourceProfile(
                sourceProfile,
                dataTypes);

            if (_isCancelled)
                return CopyResult.Cancelled();

            // Phase 4: Switch back to target profile
            ReportProgress("Switching back to target profile...", 50);
            RestoreTargetProfile(currentJobPath);

            // Phase 5: Import into current profile
            ReportProgress("Importing data...", 60);
            result = await ImportIntoCurrentProfile(
                exportedData,
                strategy);

            // Phase 6: Save changes
            ReportProgress("Saving changes...", 90);
            SaveAllChanges(dataTypes);

            ReportProgress("Complete!", 100);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            // Attempt to restore from backup
            RestoreFromBackup();
        }

        return result;
    }

    /// <summary>
    /// Export data from source profile to intermediate format.
    /// CRITICAL: This requires temporarily switching profiles.
    /// </summary>
    private async Task<ExportedDataSet> ExportFromSourceProfile(
        ProfileInfo sourceProfile,
        List<DataTypeDescriptor> dataTypes)
    {
        var exportedData = new ExportedDataSet();
        string tempExportPath = Path.Combine(
            Path.GetTempPath(),
            $"FabProfileCopy_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempExportPath);

        // CHALLENGE: Fabrication API doesn't support loading external profiles
        // SOLUTION OPTIONS:

        // Option A: Use CON file switching (requires AutoCAD restart)
        // Option B: Launch separate process that loads source profile
        // Option C: Direct .map file manipulation (risky)

        // For Phase 1, we'll use CSV export/import approach with separate process

        ReportProgress("Exporting services from source...", 20);
        if (dataTypes.Any(dt => dt.Type == DataType.Services))
        {
            exportedData.ServicesPath = await ExportServicesToCSV(
                sourceProfile,
                tempExportPath);
        }

        ReportProgress("Exporting price lists from source...", 30);
        if (dataTypes.Any(dt => dt.Type == DataType.PriceLists))
        {
            exportedData.PriceListsPath = await ExportPriceListsToCSV(
                sourceProfile,
                tempExportPath);
        }

        // ... repeat for other data types

        exportedData.TempFolder = tempExportPath;
        return exportedData;
    }

    /// <summary>
    /// Import data from intermediate format into current profile.
    /// </summary>
    private async Task<CopyResult> ImportIntoCurrentProfile(
        ExportedDataSet exportedData,
        MergeStrategy strategy)
    {
        var result = new CopyResult();

        // Import Services
        if (!string.IsNullOrEmpty(exportedData.ServicesPath))
        {
            ReportProgress("Importing services...", 65);
            var serviceResult = await ImportServicesFromCSV(
                exportedData.ServicesPath,
                strategy);
            result.AddTypeResult(DataType.Services, serviceResult);
        }

        // Import Price Lists
        if (!string.IsNullOrEmpty(exportedData.PriceListsPath))
        {
            ReportProgress("Importing price lists...", 75);
            var priceResult = await ImportPriceListsFromCSV(
                exportedData.PriceListsPath,
                strategy);
            result.AddTypeResult(DataType.PriceLists, priceResult);
        }

        // ... repeat for other data types

        return result;
    }

    private void SaveAllChanges(List<DataTypeDescriptor> dataTypes)
    {
        foreach (var dataType in dataTypes)
        {
            switch (dataType.Type)
            {
                case DataType.Services:
                    Database.SaveServices();
                    break;
                case DataType.PriceLists:
                    Database.SaveProductCosts();
                    break;
                case DataType.InstallationTimes:
                    Database.SaveInstallationTimes();
                    break;
                case DataType.Materials:
                    Database.SaveMaterials();
                    break;
                case DataType.Gauges:
                    Database.SaveGauges();
                    break;
                case DataType.Specifications:
                    Database.SaveSpecifications();
                    break;
            }
        }
    }

    public void Cancel()
    {
        _isCancelled = true;
    }

    private void ReportProgress(string message, int percentage)
    {
        _progress?.Report(new CopyProgress
        {
            Message = message,
            Percentage = percentage
        });
    }
}
```

---

### 4.4 Data Import Services (per data type)

Each data type needs a specialized import service:

**ServiceImportService.cs**
```csharp
public class ServiceImportService
{
    /// <summary>
    /// Import services from CSV file.
    /// </summary>
    public ImportResult ImportFromCSV(
        string csvPath,
        MergeStrategy strategy)
    {
        var result = new ImportResult();
        var csvData = CsvHelpers.ReadCsv(csvPath);

        foreach (var row in csvData)
        {
            try
            {
                string serviceName = row["Name"];
                string serviceGroup = row["Group"];
                int templateId = int.Parse(row["ServiceTemplateId"]);

                // Check if service already exists
                var existingService = Database.Services
                    .FirstOrDefault(s =>
                        s.Name == serviceName &&
                        s.Group == serviceGroup);

                if (existingService != null)
                {
                    switch (strategy)
                    {
                        case MergeStrategy.SkipDuplicates:
                            result.SkippedCount++;
                            continue;
                        case MergeStrategy.UpdateExisting:
                            UpdateService(existingService, row);
                            result.UpdatedCount++;
                            break;
                        case MergeStrategy.ReplaceAll:
                            // Delete and recreate
                            Database.DeleteService(existingService);
                            CreateNewService(row);
                            result.ReplacedCount++;
                            break;
                    }
                }
                else
                {
                    // Create new service
                    CreateNewService(row);
                    result.AddedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {row.LineNumber}: {ex.Message}");
            }
        }

        return result;
    }

    private void CreateNewService(CsvRow row)
    {
        string name = row["Name"];
        string group = row["Group"];
        int templateId = int.Parse(row["ServiceTemplateId"]);

        var template = Database.ServiceTemplates
            .FirstOrDefault(t => t.Id == templateId);

        if (template == null)
        {
            throw new InvalidOperationException(
                $"Service template ID {templateId} not found");
        }

        var service = new Service();
        service.Name = name;
        service.Group = group;
        service.ServiceTemplate = template;

        // Import specification if present
        if (!string.IsNullOrEmpty(row["SpecificationName"]))
        {
            var spec = Database.Specifications.FirstOrDefault(s =>
                s.Name == row["SpecificationName"] &&
                s.Group == row["SpecificationGroup"]);
            if (spec != null)
                service.Specification = spec;
        }

        Database.AddService(service);
    }

    private void UpdateService(Service service, CsvRow row)
    {
        // Update properties from CSV
        // (Implementation depends on what fields can be updated)
    }
}
```

**PriceListImportService.cs** - Similar pattern for price lists

**MaterialImportService.cs** - Similar pattern for materials

---

### 4.5 Supporting Models

**ProfileInfo.cs**
```csharp
public class ProfileInfo
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string DatabasePath { get; set; }
    public string Version { get; set; }
    public bool IsCurrent { get; set; }

    public override string ToString() =>
        $"{Name} ({Version})";
}
```

**DataTypeDescriptor.cs**
```csharp
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

public class DataTypeDescriptor
{
    public DataType Type { get; set; }
    public string DisplayName { get; set; }
    public string MapFileName { get; set; }
    public bool IsSelected { get; set; }
    public int ItemCount { get; set; }
    public string Description { get; set; }
}
```

**MergeStrategy.cs**
```csharp
public enum MergeStrategy
{
    /// <summary>Add new items, skip existing</summary>
    SkipDuplicates,

    /// <summary>Add new items, update existing by matching ID/name</summary>
    UpdateExisting,

    /// <summary>Clear all existing, import fresh</summary>
    ReplaceAll,

    /// <summary>Add all as new (may create duplicates)</summary>
    AppendAll
}

public class MergeOptions
{
    public MergeStrategy Strategy { get; set; }
    public bool CreateBackup { get; set; } = true;
    public bool ValidateBeforeImport { get; set; } = true;
    public bool StopOnFirstError { get; set; } = false;
}
```

**CopyResult.cs**
```csharp
public class CopyResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public Dictionary<DataType, ImportResult> TypeResults { get; set; }
    public TimeSpan Duration { get; set; }
    public string BackupPath { get; set; }

    public int TotalAdded => TypeResults.Values.Sum(r => r.AddedCount);
    public int TotalUpdated => TypeResults.Values.Sum(r => r.UpdatedCount);
    public int TotalSkipped => TypeResults.Values.Sum(r => r.SkippedCount);
    public int TotalErrors => TypeResults.Values.Sum(r => r.Errors.Count);

    public static CopyResult Cancelled()
    {
        return new CopyResult
        {
            Success = false,
            ErrorMessage = "Operation was cancelled by user"
        };
    }
}

public class ImportResult
{
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int ReplacedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
```

---

## 5. CRITICAL TECHNICAL CHALLENGES

### 5.1 Multi-Profile Access Limitation

**Problem**: Fabrication API can only load one profile at a time in a process.

**Solutions Evaluated**:

| Approach | Pros | Cons | Selected |
|----------|------|------|----------|
| **A. Separate Process** | Clean separation, safe | Complex IPC, slow | ✓ Yes (Phase 1) |
| **B. Profile Switching** | Uses existing API | Requires CADmep restart | No |
| **C. Direct .map Parsing** | Fast, no switching | Risky, proprietary format | No |
| **D. Copy .map Files** | Simple | No merge, overwrites all | No |

**Implementation**: Separate Process Approach
```csharp
// Launch helper process that loads source profile and exports to CSV
ProcessStartInfo psi = new ProcessStartInfo
{
    FileName = "FabProfileExporter.exe",
    Arguments = $"\"{sourceProfile.Path}\" \"{tempExportPath}\"",
    UseShellExecute = false,
    CreateNoWindow = true
};

Process exportProcess = Process.Start(psi);
await exportProcess.WaitForExitAsync();

// Now import the exported CSVs into current profile
```

### 5.2 Relationship Integrity

**Problem**: Services reference Materials, Gauges, and Specifications by ID. If IDs differ between profiles, references break.

**Solution**: Two-phase import
1. Import foundational data first (Materials, Gauges, Specs)
2. Build ID mapping table (source ID → target ID)
3. Import dependent data (Services) using ID mapping
4. Remap references during import

```csharp
// Example: Material ID mapping
var materialMapping = new Dictionary<int, int>();

foreach (var sourceMaterial in importedMaterials)
{
    var targetMaterial = Database.Materials
        .FirstOrDefault(m => m.Name == sourceMaterial.Name);

    if (targetMaterial != null)
    {
        // Use existing
        materialMapping[sourceMaterial.SourceId] = targetMaterial.Id;
    }
    else
    {
        // Create new
        var newMaterial = Database.AddMaterial(sourceMaterial.Name);
        materialMapping[sourceMaterial.SourceId] = newMaterial.Id;
    }
}

// Later, when importing service:
int sourceMaterialId = csvRow["MaterialId"];
int targetMaterialId = materialMapping[sourceMaterialId];
service.Material = Database.Materials.First(m => m.Id == targetMaterialId);
```

### 5.3 Duplicate Detection

**Problem**: Determining if an item "already exists" is not straightforward.

**Strategies**:
- **Services**: Match by Name + Group
- **Materials**: Match by Name
- **Gauges**: Match by Name
- **Price Lists**: Match by Supplier Group Name + Price List Name
- **Specifications**: Match by Name + Group

### 5.4 Binary .map File Format

**Problem**: Cannot directly read/write .map files without Fabrication API.

**Workaround**: Always use API methods:
- `Database.Save*()` methods to write
- `Database.*` collections to read
- Export to intermediate CSV for cross-profile transfer

---

## 6. IMPLEMENTATION PLAN

### Phase 1: Core Infrastructure (Week 1-2)

**Tasks**:
1. Create ProfileInfo and supporting models
2. Implement ProfileDiscoveryService
3. Create basic ProfileDataCopyWindow UI
4. Implement BackupHelper utility
5. Build separate console app "FabProfileExporter.exe"

**Deliverable**: UI that discovers profiles and creates backups

### Phase 2: Single Data Type (Week 3)

**Tasks**:
1. Implement Service export in FabProfileExporter.exe
2. Implement ServiceImportService with all merge strategies
3. Test service import with various scenarios
4. Handle duplicate detection and ID mapping

**Deliverable**: Working service copy functionality

### Phase 3: Additional Data Types (Week 4-5)

**Tasks**:
1. Implement Materials import/export
2. Implement Gauges import/export
3. Implement Specifications import/export
4. Implement Price Lists import/export
5. Implement Installation Times import/export

**Deliverable**: Full data type coverage

### Phase 4: Relationship Handling (Week 6)

**Tasks**:
1. Build ID mapping infrastructure
2. Implement dependency ordering
3. Test cross-references (Service → Material, etc.)
4. Handle missing dependencies gracefully

**Deliverable**: Robust relationship preservation

### Phase 5: UI Polish & Validation (Week 7)

**Tasks**:
1. Add preview functionality
2. Implement progress reporting
3. Add validation before import
4. Improve error messages
5. Add import summary dialog

**Deliverable**: Production-ready UI

### Phase 6: Testing & Documentation (Week 8)

**Tasks**:
1. Test with real-world profiles
2. Test edge cases (empty profiles, huge profiles)
3. Write user documentation
4. Create video tutorial
5. Code review and refactoring

**Deliverable**: Tested, documented feature

---

## 7. USER WORKFLOWS

### 7.1 Copy Services from Another Profile

1. User opens FabricationSample
2. Clicks "Database Editor" tab
3. Clicks new "Import from Profile" button
4. Selects source profile from dropdown
5. Checks "Services" checkbox
6. Selects merge strategy: "Skip Duplicates"
7. Clicks "Preview Changes"
   - Sees: "45 services found, 12 new, 33 duplicates"
8. Clicks "Start Import"
9. Progress bar shows: "Exporting from source... Importing... Saving..."
10. Summary dialog shows: "12 services added, 33 skipped"
11. User clicks "OK"
12. Services now visible in Services tab

### 7.2 Bootstrap New Profile

1. User has empty new profile
2. Wants to copy everything from production profile
3. Opens Profile Data Copy
4. Selects production profile as source
5. Clicks "Select All" data types
6. Chooses merge strategy: "Replace All" (safe since target is empty)
7. Enables "Create backup" (always recommended)
8. Clicks "Start Import"
9. Waits 2-5 minutes for large import
10. Summary shows complete data transfer
11. New profile now has all configuration

### 7.3 Merge Price Lists

1. User wants to add price lists from regional profile
2. Opens Profile Data Copy
3. Selects regional profile
4. Checks only "Price Lists"
5. Chooses "Update Existing" strategy
6. Clicks "Preview"
   - Sees which lists are new vs updated
7. Reviews changes, clicks "Start Import"
8. Price lists merged successfully

---

## 8. ALTERNATIVE APPROACHES (NOT SELECTED)

### 8.1 Database File Copying

**Concept**: Simply copy .map files from source to target

**Pros**:
- Very fast
- Simple implementation

**Cons**:
- No merge capability - all or nothing
- Could corrupt database if versions mismatch
- No validation
- Overwrites everything

**Verdict**: Too risky, not user-friendly

### 8.2 Registry-Based Profile Switching

**Concept**: Modify Windows Registry to change active profile, restart CADmep

**Pros**:
- Uses official profile switching mechanism

**Cons**:
- Requires CADmep restart (user disruption)
- Registry manipulation is risky
- Very slow for multiple data types
- Complex error recovery

**Verdict**: Too disruptive to user workflow

### 8.3 Network Database Sharing

**Concept**: Point both profiles at same network database location

**Pros**:
- Real-time sharing
- No import needed

**Cons**:
- Not a copy feature - would affect both profiles
- Requires network infrastructure
- Potential concurrency issues

**Verdict**: Different feature entirely

---

## 9. TESTING STRATEGY

### 9.1 Unit Tests

```csharp
[TestClass]
public class ServiceImportServiceTests
{
    [TestMethod]
    public void ImportServices_SkipDuplicates_SkipsExisting()
    {
        // Arrange
        var existingService = CreateTestService("HVAC", "Group1");
        Database.AddService(existingService);

        var csvData = CreateCSVWithService("HVAC", "Group1");

        // Act
        var result = serviceImportService.ImportFromCSV(
            csvData,
            MergeStrategy.SkipDuplicates);

        // Assert
        Assert.AreEqual(0, result.AddedCount);
        Assert.AreEqual(1, result.SkippedCount);
    }

    [TestMethod]
    public void ImportServices_UpdateExisting_UpdatesProperties()
    {
        // Test update logic
    }
}
```

### 9.2 Integration Tests

- Test full profile copy with real .map files
- Test with profiles from different Fabrication versions
- Test with very large profiles (10,000+ items)
- Test error recovery (power loss simulation)

### 9.3 User Acceptance Tests

- Copy services between real profiles
- Verify no data corruption
- Verify performance (< 5 minutes for typical profile)
- Verify backup/restore works

---

## 10. FUTURE ENHANCEMENTS

### 10.1 Profile Comparison Tool

Show side-by-side comparison of two profiles:
- Services present in A but not B
- Price list differences
- Material variances

### 10.2 Selective Item Copy

Instead of all services, select specific services:
- Checkboxes for individual services
- Filter by group
- Search functionality

### 10.3 Scheduled Sync

Automatically sync profiles on schedule:
- Monitor source profile for changes
- Auto-import changes to target
- Email notifications

### 10.4 Cloud Profile Repository

- Upload profiles to cloud storage
- Share profiles across organization
- Version control for profiles

---

## 11. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Data corruption | Medium | High | Always create backup, extensive testing |
| Profile version mismatch | Low | Medium | Check version compatibility before import |
| Performance issues (large profiles) | Medium | Medium | Async operations, progress reporting |
| ID conflicts | High | Medium | ID mapping infrastructure |
| User cancels mid-operation | Medium | High | Transaction-like rollback from backup |
| Missing dependencies | Medium | Medium | Import in dependency order, validation |

---

## 12. SUCCESS METRICS

- Import completes in < 5 minutes for typical profile (1000 items)
- Zero data corruption incidents in testing
- 95%+ user satisfaction in UAT
- Backup/restore works in 100% of failure scenarios
- Handles profiles up to 50,000 items
- Supports Fabrication 2024, 2025, 2026

---

## 13. INTEGRATION WITH EXISTING FEATURES

### 13.1 DatabaseEditor Integration

Add new button to Services tab:
```xml
<Button Content="Import from Profile"
        Click="ImportFromProfile_Click"
        ToolTip="Copy services from another profile"/>
```

Similar buttons on:
- Price Lists tab
- Materials tab
- Installation Times tab

### 13.2 NETLOAD Command

Add quick command for power users:
```csharp
[CommandMethod("ImportProfileData")]
public static void ImportProfileData()
{
    var window = new ProfileDataCopyWindow();
    window.ShowDialog();
}
```

### 13.3 Menu Integration

Add to FabricationWindow menu:
```
File
  ├── Import from Profile...  [NEW]
  ├── Export Current Profile...
  └── Backup Database...
```

---

## 14. CODE STRUCTURE SUMMARY

```
FabricationSample/
├── Services/
│   ├── ProfileCopy/
│   │   ├── ProfileDataCopyService.cs          (Main orchestrator)
│   │   ├── ProfileDiscoveryService.cs         (Find profiles)
│   │   ├── ProfileSwitchingService.cs         (Manage switching)
│   │   └── DataMergeService.cs                (Merge logic)
│   └── Import/
│       ├── ServiceImportService.cs            (Service imports)
│       ├── PriceListImportService.cs          (Price list imports)
│       ├── MaterialImportService.cs           (Material imports)
│       └── [other import services]
│
├── Models/
│   ├── ProfileCopy/
│   │   ├── ProfileInfo.cs
│   │   ├── DataTypeDescriptor.cs
│   │   ├── MergeStrategy.cs
│   │   ├── CopyResult.cs
│   │   └── ExportedDataSet.cs
│
├── Windows/
│   └── ProfileDataCopyWindow.xaml/cs          (Main UI)
│
├── UserControls/
│   ├── DatabaseEditor/
│   │   └── DatabaseEditor-ProfileCopy.cs      (Integration point)
│
├── Utilities/
│   ├── ProfilePathHelper.cs                   (Path utilities)
│   └── BackupHelper.cs                        (Backup operations)
│
└── External/
    └── FabProfileExporter/                    (Separate console app)
        └── Program.cs                         (Exports from source profile)
```

---

## 15. CONCLUSION

This Profile Data Copy feature addresses a critical gap in Fabrication workflow tooling. By enabling selective, validated cross-profile data transfer with proper merge strategies, it will save users significant time and reduce errors compared to manual .map file manipulation.

The phased implementation approach ensures we deliver value early (services) while building toward comprehensive coverage. The use of a separate exporter process solves the fundamental API limitation while maintaining data integrity.

**Recommendation**: Proceed with Phase 1 implementation to validate architecture and user workflows.
