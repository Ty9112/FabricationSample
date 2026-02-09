# Profile Data Copy - Quick Start Guide

## What This Feature Does

Allows users to copy database configuration data (services, price lists, materials, etc.) from one Fabrication profile to another through a user-friendly UI.

## Architecture Summary

```
┌─────────────────────────────────────────────────────────────┐
│                    USER INTERACTION                         │
│                                                             │
│  ProfileDataCopyWindow.xaml                                 │
│  ┌───────────────────────────────────────────────────┐    │
│  │  [Source Profile Dropdown ▼]                       │    │
│  │                                                     │    │
│  │  ☑ Services          ☑ Materials                  │    │
│  │  ☑ Price Lists       ☑ Gauges                     │    │
│  │  ☑ Install Times     ☑ Specifications             │    │
│  │                                                     │    │
│  │  ○ Skip Duplicates  ○ Update  ○ Replace All       │    │
│  │                                                     │    │
│  │  [Preview]  [Start Import]  [Cancel]               │    │
│  └───────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                   SERVICE LAYER                             │
│                                                             │
│  ProfileDataCopyService (orchestrator)                      │
│  ┌───────────────────────────────────────────────────┐    │
│  │ 1. Create backup                                   │    │
│  │ 2. Export from source profile → CSV files         │    │
│  │ 3. Import into current profile from CSV           │    │
│  │ 4. Apply merge strategy                           │    │
│  │ 5. Save changes                                    │    │
│  └───────────────────────────────────────────────────┘    │
│                                                             │
│  Supporting Services:                                       │
│  • ProfileDiscoveryService (find profiles)                  │
│  • ServiceImportService (import services)                   │
│  • PriceListImportService (import price lists)              │
│  • MaterialImportService (import materials)                 │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                   DATA LAYER                                │
│                                                             │
│  Fabrication API Access:                                    │
│  • Database.Services                                        │
│  • Database.SupplierGroups                                  │
│  • Database.Materials                                       │
│  • Database.Save*() methods                                 │
│                                                             │
│  File System:                                               │
│  • C:\ProgramData\Autodesk\Fabrication\<VER>\<PROFILE>\    │
│    └── DATABASE\                                            │
│        ├── service.map                                      │
│        ├── costs.map                                        │
│        ├── material.map                                     │
│        └── ...                                              │
└─────────────────────────────────────────────────────────────┘
```

## Key Technical Concepts

### 1. The Multi-Profile Challenge

**Problem**: Fabrication API can only load ONE profile at a time.

**Solution**: Use intermediate CSV export/import
1. Export from source profile → CSV files
2. Import from CSV files → current profile

### 2. Data Flow Sequence

```
┌──────────────┐
│ User selects │
│ source       │
│ profile      │
└──────┬───────┘
       │
       ▼
┌──────────────────────────────────────┐
│ ProfileDiscoveryService              │
│ Scans file system for profiles       │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ User selects data types & options    │
│ - Services, Materials, etc.          │
│ - Merge strategy                     │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ BackupHelper creates backup ZIP      │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ CHALLENGE: How to access source?    │
│                                      │
│ Option A (Phase 1):                  │
│ → Separate process loads source     │
│ → Exports to CSV                     │
│ → Main process imports CSV           │
│                                      │
│ Option B (Future):                   │
│ → Direct .map file parsing           │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ ImportService merges data            │
│ - Check for duplicates               │
│ - Apply merge strategy               │
│ - Preserve relationships             │
└──────┬───────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────┐
│ Database.Save*() persists changes    │
└──────────────────────────────────────┘
```

### 3. Merge Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Skip Duplicates** | Add new only | Safest - no overwrites |
| **Update Existing** | Add + overwrite | Sync changes |
| **Replace All** | Delete + import | Bootstrap new profile |
| **Append All** | Add everything | Testing, may create duplicates |

### 4. Duplicate Detection Rules

- **Services**: Match by Name + Group
- **Materials**: Match by Name
- **Gauges**: Match by Name
- **Specifications**: Match by Name + Group
- **Price Lists**: Match by Supplier Group + List Name

### 5. Relationship Handling

Services reference other data:
```csharp
Service
  ├─ ServiceTemplate (by ID)
  ├─ Specification (by ID)
  └─ (via template) → Material, Gauge, etc.
```

Import order matters:
1. Materials (no dependencies)
2. Gauges (no dependencies)
3. Specifications (no dependencies)
4. Service Templates (references materials, gauges)
5. Services (references templates, specs)
6. Price Lists (references products)

## File Structure

```
FabricationSample/
├── Models/
│   └── ProfileCopy/
│       ├── ProfileInfo.cs               (Profile metadata)
│       ├── DataTypeDescriptor.cs        (Data type info)
│       ├── MergeStrategy.cs             (Merge options)
│       └── CopyResult.cs                (Results & progress)
│
├── Services/
│   └── ProfileCopy/
│       ├── ProfileDataCopyService.cs    (Main orchestrator)
│       ├── ProfileDiscoveryService.cs   (Find profiles)
│       └── Import/
│           ├── ServiceImportService.cs
│           ├── PriceListImportService.cs
│           └── MaterialImportService.cs
│
├── Windows/
│   └── ProfileDataCopyWindow.xaml/cs    (Main UI)
│
├── Utilities/
│   ├── ProfilePathHelper.cs             (Path resolution)
│   └── BackupHelper.cs                  (Backup/restore)
│
└── UserControls/
    └── DatabaseEditor/
        └── DatabaseEditor.xaml.cs       (Add "Import" button)
```

## Implementation Phases

### Phase 1: Infrastructure (CURRENT)
- ✓ Create all model classes
- ✓ Implement ProfileDiscoveryService
- ✓ Build ProfileDataCopyWindow UI
- ✓ Implement BackupHelper
- ○ Test UI with profile discovery

**Deliverable**: UI that discovers profiles, no actual import yet

### Phase 2: Service Import
- Implement ServiceImportService
- Handle duplicate detection
- Test all merge strategies
- Add to main UI

**Deliverable**: Working service copy

### Phase 3: Additional Data Types
- Implement import services for:
  - Materials
  - Gauges
  - Specifications
  - Price Lists
  - Installation Times

**Deliverable**: Full data type coverage

### Phase 4: Polish
- Add preview functionality
- Improve progress reporting
- Add validation
- Error handling
- User documentation

**Deliverable**: Production ready

## Integration Points

### 1. Database Editor Button

Add button to DatabaseEditor.xaml Services tab:

```xml
<Button Content="Import from Profile..."
        Click="ImportFromProfile_Click"
        ToolTip="Copy services from another profile"
        Width="150"/>
```

Code-behind:
```csharp
private void ImportFromProfile_Click(object sender, RoutedEventArgs e)
{
    var window = new ProfileDataCopyWindow();
    window.Owner = Window.GetWindow(this);
    if (window.ShowDialog() == true)
    {
        // Refresh services display
        LoadServices(null);
    }
}
```

### 2. NETLOAD Command

Add command for power users:

```csharp
[CommandMethod("ImportProfileData")]
public static void ImportProfileData()
{
    var window = new ProfileDataCopyWindow();
    var mainWindow = Process.GetCurrentProcess().MainWindowHandle;
    new WindowInteropHelper(window).Owner = mainWindow;
    window.ShowDialog();
}
```

### 3. Main Menu

Add to FabricationWindow menu structure (if exists).

## Usage Examples

### Example 1: Copy Services Only

```
1. User: Click "Import from Profile" button in Services tab
2. Window: Shows available profiles
3. User: Select "Production_Profile_2024"
4. User: Check only "Services"
5. User: Select "Skip Duplicates"
6. User: Click "Start Import"
7. System: Creates backup
8. System: Imports 45 services (12 new, 33 skipped)
9. User: Sees updated service list
```

### Example 2: Bootstrap New Profile

```
1. Admin creates new empty profile "Regional_West"
2. Loads profile in CADmep
3. Opens FabricationSample
4. Clicks "Import from Profile"
5. Selects "Production_Master" as source
6. Clicks "Select All" data types
7. Chooses "Replace All" (safe - target is empty)
8. Clicks "Start Import"
9. Waits 3 minutes for full import
10. New profile now has complete configuration
```

### Example 3: Sync Price Updates

```
1. Corporate updates master price list monthly
2. Regional user wants latest prices
3. Opens import dialog
4. Selects "Corporate_Master_2025"
5. Checks only "Price Lists"
6. Selects "Update Existing"
7. Clicks "Preview" - sees 156 updates, 12 new
8. Confirms import
9. Price lists now synced
```

## Testing Checklist

### Phase 1 Tests
- [ ] ProfileDiscoveryService finds all profiles
- [ ] Current profile is excluded from list
- [ ] Profile dropdown displays correctly
- [ ] Data type checkboxes bind correctly
- [ ] Select All / None buttons work
- [ ] Merge strategy radio buttons work
- [ ] BackupHelper creates valid ZIP
- [ ] Backup can be restored
- [ ] UI handles empty profile list gracefully

### Phase 2 Tests (Service Import)
- [ ] Services import with Skip Duplicates
- [ ] Services import with Update Existing
- [ ] Services import with Replace All
- [ ] Duplicate detection works correctly
- [ ] Service templates are preserved
- [ ] Specifications are linked correctly
- [ ] Progress updates display
- [ ] Errors are handled gracefully

### Integration Tests
- [ ] Import from 2024 to 2025 profile
- [ ] Import from 2025 to 2024 profile
- [ ] Import with 1000+ items
- [ ] Import interrupted - recovers from backup
- [ ] Multiple imports in same session
- [ ] Import while job is loaded

## Common Issues & Solutions

### Issue: No profiles found

**Cause**: Profiles in non-standard location

**Solution**: Update `ProfilePathHelper.GetFabricationBasePaths()` to include custom paths

### Issue: Import fails mid-operation

**Cause**: Various (file locks, permissions, API errors)

**Solution**: Backup is automatically created - restore from backup

### Issue: Duplicate services created

**Cause**: Name matching failed due to whitespace/case

**Solution**: Normalize names before comparison:
```csharp
string Normalize(string s) => s?.Trim().ToLowerInvariant() ?? "";
```

### Issue: Relationships broken after import

**Cause**: ID mapping not applied

**Solution**: Build ID mapping dictionary during import

## Future Enhancements

1. **Profile Comparison View**
   - Side-by-side comparison
   - Highlight differences
   - Selective import

2. **Scheduled Sync**
   - Auto-sync on schedule
   - Email notifications
   - Change detection

3. **Cloud Profile Storage**
   - Upload/download profiles
   - Share across organization
   - Version control

4. **Bulk Operations**
   - Import to multiple targets
   - Export from multiple sources
   - Batch processing

## Performance Expectations

| Operation | Items | Expected Time |
|-----------|-------|---------------|
| Backup creation | All | 5-10 seconds |
| Profile discovery | 10 profiles | < 1 second |
| Service import | 100 | 10-15 seconds |
| Service import | 1000 | 60-90 seconds |
| Price list import | 50 lists | 30-45 seconds |
| Full profile copy | All data | 2-5 minutes |

## Security Considerations

1. **Backup before import**: Always enabled by default
2. **Validation**: Check data integrity before applying
3. **Permissions**: Ensure user has write access to database folder
4. **Audit trail**: Log all imports with timestamp and user

## Support & Documentation

- Design Doc: `PROFILE_DATA_COPY_DESIGN.md`
- Implementation: `PROFILE_COPY_IMPLEMENTATION_PLAN.md`
- This Guide: `PROFILE_COPY_QUICKSTART.md`

## Getting Started

1. Read this Quick Start
2. Review Design Document for detailed architecture
3. Follow Implementation Plan for step-by-step coding
4. Start with Phase 1 (infrastructure)
5. Test thoroughly before moving to Phase 2
