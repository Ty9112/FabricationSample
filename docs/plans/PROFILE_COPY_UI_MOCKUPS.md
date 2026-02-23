# Profile Data Copy - UI Mockups & User Experience

## Main Window - ProfileDataCopyWindow

### Initial State

```
┌─────────────────────────────────────────────────────────────────┐
│  Copy Data from Another Profile                           [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Import Database Configuration from Another Profile              │
│  Select a source profile and choose which data types to import. │
│                                                                  │
│  ┌─ Source Profile ──────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  [Select source profile                            ▼]     │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Data Types to Import ────────────────────────────────────┐ │
│  │                                                            │ │
│  │  [Select All]  [Select None]                              │ │
│  │                                                            │ │
│  │  ☐ Services                                               │ │
│  │  ☐ Price Lists                                            │ │
│  │  ☐ Installation Times                                     │ │
│  │  ☐ Materials                                              │ │
│  │  ☐ Gauges                                                 │ │
│  │  ☐ Specifications                                         │ │
│  │  ☐ Custom Data Definitions                                │ │
│  │  ☐ Service Templates                                      │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Import Options ───────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  How should duplicates be handled?                        │ │
│  │                                                            │ │
│  │  ● Skip Duplicates (recommended)                          │ │
│  │  ○ Update Existing                                        │ │
│  │  ○ Replace All                                            │ │
│  │                                                            │ │
│  │  ─────────────────────────────────────────────────────    │ │
│  │                                                            │ │
│  │  ☑ Create backup before import                            │ │
│  │  ☑ Validate data before importing                         │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│                   [Preview Changes]  [Start Import]  [Cancel]   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Profile Selected State

```
┌─────────────────────────────────────────────────────────────────┐
│  Copy Data from Another Profile                           [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Import Database Configuration from Another Profile              │
│  Select a source profile and choose which data types to import. │
│                                                                  │
│  ┌─ Source Profile ──────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  [Production_Master (Fabrication 2025)           ▼]       │ │
│  │                                                            │ │
│  │  Path: C:\ProgramData\Autodesk\Fabrication\2025\Production │
│  │        _Master                                             │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Data Types to Import ────────────────────────────────────┐ │
│  │                                                            │ │
│  │  [Select All]  [Select None]                              │ │
│  │                                                            │ │
│  │  ☑ Services (145 items)                                   │ │
│  │  ☑ Price Lists (23 supplier groups)                       │ │
│  │  ☐ Installation Times (18 tables)                         │ │
│  │  ☑ Materials (342 items)                                  │ │
│  │  ☑ Gauges (45 items)                                      │ │
│  │  ☑ Specifications (28 items)                              │ │
│  │  ☐ Custom Data Definitions (7 items)                      │ │
│  │  ☐ Service Templates (12 items)                           │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Import Options ───────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  How should duplicates be handled?                        │ │
│  │                                                            │ │
│  │  ● Skip Duplicates (recommended)                          │ │
│  │    Add new items only, skip items that already exist      │ │
│  │                                                            │ │
│  │  ○ Update Existing                                        │ │
│  │    Add new items and update existing items with new data  │ │
│  │                                                            │ │
│  │  ○ Replace All                                            │ │
│  │    Delete all existing items and import fresh             │ │
│  │                                                            │ │
│  │  ─────────────────────────────────────────────────────    │ │
│  │                                                            │ │
│  │  ☑ Create backup before import                            │ │
│  │  ☑ Validate data before importing                         │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│                   [Preview Changes]  [Start Import]  [Cancel]   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### During Import (Progress)

```
┌─────────────────────────────────────────────────────────────────┐
│  Copy Data from Another Profile                           [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Import Database Configuration from Another Profile              │
│  Select a source profile and choose which data types to import. │
│                                                                  │
│  ┌─ Source Profile ──────────────────────────────────────────┐ │
│  │  [Production_Master (Fabrication 2025)           ▼]       │ │
│  │  Path: C:\ProgramData\Autodesk\Fabrication\2025\...       │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Data Types to Import ────────────────────────────────────┐ │
│  │  [Select All]  [Select None]                              │ │
│  │  ☑ Services (145 items)                                   │ │
│  │  ☑ Price Lists (23 supplier groups)                       │ │
│  │  ☑ Materials (342 items)                                  │ │
│  │  ☑ Gauges (45 items)                                      │ │
│  │  ☑ Specifications (28 items)                              │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Import Options ───────────────────────────────────────────┐ │
│  │  ● Skip Duplicates (recommended)                          │ │
│  │  ☑ Create backup before import                            │ │
│  │  ☑ Validate data before importing                         │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Progress ─────────────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░ 65%                       │ │
│  │                                                            │ │
│  │  Importing materials... (223 of 342)                      │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│                                                       [Cancel]   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Import Complete Summary

```
┌─────────────────────────────────────────────────────────────────┐
│  Import Complete                                          [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ✓ Import completed successfully in 45.3 seconds.               │
│                                                                  │
│  Summary:                                                        │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Services:                                                       │
│    • Added: 48 new services                                     │
│    • Skipped: 97 existing services                              │
│                                                                  │
│  Price Lists:                                                    │
│    • Added: 5 new supplier groups                               │
│    • Added: 23 new price lists                                  │
│    • Skipped: 18 existing groups                                │
│                                                                  │
│  Materials:                                                      │
│    • Added: 156 new materials                                   │
│    • Skipped: 186 existing materials                            │
│                                                                  │
│  Gauges:                                                         │
│    • Added: 12 new gauges                                       │
│    • Skipped: 33 existing gauges                                │
│                                                                  │
│  Specifications:                                                 │
│    • Added: 8 new specifications                                │
│    • Skipped: 20 existing specifications                        │
│                                                                  │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Total Added: 229                                                │
│  Total Skipped: 354                                              │
│  Total Errors: 0                                                 │
│                                                                  │
│  Backup created at:                                              │
│  C:\Users\...\AppData\Local\FabricationSample\Backups\          │
│  Backup_Regional_West_20260116_143022.zip                       │
│                                                                  │
│                                                         [OK]     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Integration into DatabaseEditor

### Services Tab - Before

```
┌─ Services ──────────────────────────────────────────────────────┐
│                                                                  │
│  Service: [HVAC Supply                              ▼]          │
│                                                                  │
│  [Edit Name]  [Add Service]  [Delete Service]  [Save Services]  │
│                                                                  │
│  ...                                                             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Services Tab - After (with Import button)

```
┌─ Services ──────────────────────────────────────────────────────┐
│                                                                  │
│  Service: [HVAC Supply                              ▼]          │
│                                                                  │
│  [Edit Name]  [Add Service]  [Delete Service]                   │
│                                                                  │
│  [Import from Profile...]  [Save Services]                      │
│                                                                  │
│  ...                                                             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Price Lists Tab - After

```
┌─ Price Lists ───────────────────────────────────────────────────┐
│                                                                  │
│  Supplier Group: [Main Suppliers                  ▼]            │
│  Price List: [Material Costs 2025                 ▼]            │
│                                                                  │
│  [Add Entry]  [Add List]  [Delete List]                         │
│                                                                  │
│  [Import from Profile...]  [Update Prices]                      │
│                                                                  │
│  ...                                                             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

## Preview Dialog (Future Enhancement)

```
┌─────────────────────────────────────────────────────────────────┐
│  Preview Import Changes                                   [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  The following changes will be made:                             │
│                                                                  │
│  ┌─ Services ────────────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  ✓ HVAC Supply - Supply Air      [NEW]                    │ │
│  │  ✓ HVAC Return - Return Air      [NEW]                    │ │
│  │  ✓ HVAC Exhaust - Exhaust Air    [NEW]                    │ │
│  │  ○ Plumbing - Cold Water         [EXISTS - SKIP]          │ │
│  │  ○ Plumbing - Hot Water          [EXISTS - SKIP]          │ │
│  │  ...                                                       │ │
│  │                                                            │ │
│  │  Summary: 48 new, 97 existing (skipped)                   │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─ Materials ───────────────────────────────────────────────┐ │
│  │                                                            │ │
│  │  ✓ Galvanized Steel - 24ga       [NEW]                    │ │
│  │  ✓ Galvanized Steel - 22ga       [NEW]                    │ │
│  │  ○ Stainless Steel - 20ga        [EXISTS - SKIP]          │ │
│  │  ...                                                       │ │
│  │                                                            │ │
│  │  Summary: 156 new, 186 existing (skipped)                 │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ⚠ Warnings:                                                     │
│    • Service "HVAC Supply" references material "304 SS" which   │
│      does not exist in target profile                           │
│                                                                  │
│                          [Continue Import]  [Cancel]            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Error Scenarios

### No Profiles Found

```
┌─────────────────────────────────────────────────────────────────┐
│  No Profiles Found                                        [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ⓘ No other Fabrication profiles found on this system.          │
│                                                                  │
│  Profile locations checked:                                      │
│    • C:\ProgramData\Autodesk\Fabrication\2024                   │
│    • C:\ProgramData\Autodesk\Fabrication\2025                   │
│    • C:\ProgramData\Autodesk\Fabrication\2026                   │
│                                                                  │
│  To use this feature, you need at least two Fabrication         │
│  profiles installed on this system.                             │
│                                                                  │
│                                                         [OK]     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Import Failed

```
┌─────────────────────────────────────────────────────────────────┐
│  Import Failed                                            [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ✗ Import failed: Unable to access source profile database.     │
│                                                                  │
│  Error Details:                                                  │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Access denied to:                                               │
│  C:\ProgramData\Autodesk\Fabrication\2025\Production_Master\    │
│  DATABASE\service.map                                            │
│                                                                  │
│  The file may be locked by another process or you may not       │
│  have sufficient permissions.                                    │
│                                                                  │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Your database has been restored from backup:                   │
│  C:\Users\...\AppData\Local\FabricationSample\Backups\          │
│  Backup_Regional_West_20260116_143022.zip                       │
│                                                                  │
│  No changes were made to your current profile.                  │
│                                                                  │
│                                    [View Log]         [OK]      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Partial Import Success

```
┌─────────────────────────────────────────────────────────────────┐
│  Import Completed with Warnings                           [X]   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ⚠ Import completed with 3 errors.                              │
│                                                                  │
│  Summary:                                                        │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Services:                                                       │
│    • Added: 45 new services                                     │
│    • Skipped: 97 existing services                              │
│    • Errors: 3 services failed                                  │
│                                                                  │
│  Materials:                                                      │
│    • Added: 156 new materials                                   │
│    • Skipped: 186 existing materials                            │
│                                                                  │
│  ────────────────────────────────────────────────────────────   │
│                                                                  │
│  Errors:                                                         │
│    1. Service "Special HVAC" - Missing service template ID 999  │
│    2. Service "Custom Return" - Invalid specification reference │
│    3. Service "Test Service" - Duplicate name conflict          │
│                                                                  │
│  Failed items were skipped. All other data was imported         │
│  successfully.                                                   │
│                                                                  │
│  Backup created at:                                              │
│  C:\Users\...\AppData\Local\FabricationSample\Backups\          │
│  Backup_Regional_West_20260116_143022.zip                       │
│                                                                  │
│                                    [View Log]         [OK]      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Tooltips

### Data Type Checkboxes

**Services**: "Service definitions with specifications and templates"

**Price Lists**: "Supplier groups and price lists for product costing"

**Installation Times**: "Installation time tables and labor rates"

**Materials**: "Material definitions used in fabrication items"

**Gauges**: "Gauge and thickness definitions for materials"

**Specifications**: "Specification groups for service assignments"

**Custom Data**: "Custom data field definitions for items"

**Service Templates**: "Service template configurations"

### Merge Strategies

**Skip Duplicates**: "Add only new items that don't already exist in the target profile. Safest option - no data will be overwritten."

**Update Existing**: "Add new items AND update existing items with data from source profile. Use when synchronizing changes."

**Replace All**: "Delete ALL existing items and import fresh from source. Use only when bootstrapping a new profile."

### Options

**Create backup**: "Always recommended. Creates a ZIP backup of your current database before making any changes."

**Validate data**: "Check imported data for errors before applying. Recommended to prevent corruption."

## Keyboard Shortcuts

- **Ctrl+A**: Select all data types
- **Ctrl+Shift+A**: Select none
- **Ctrl+Enter**: Start import (when enabled)
- **Escape**: Cancel/Close
- **F1**: Help (future)

## Accessibility Features

- All controls keyboard navigable
- Screen reader friendly labels
- High contrast support
- Tooltips on all interactive elements
- Progress updates announced
- Error messages clear and actionable

## Responsive Behavior

### Window Sizes

**Minimum**: 600 x 650 pixels
**Default**: 600 x 650 pixels
**Maximum**: Fixed (no resize) for Phase 1

Future: Resizable with scrollable data type list for more types

## Color Coding (Future)

- **Green text**: New items to be added
- **Gray text**: Existing items to be skipped
- **Orange text**: Items to be updated
- **Red text**: Items with errors
- **Blue text**: Dependencies/relationships

## State Management

### Button States

| Button | Enabled When | Disabled When |
|--------|--------------|---------------|
| Start Import | Profile selected AND at least one data type checked | No profile OR no data types |
| Preview | Same as Start Import | Same as Start Import |
| Cancel | Always | During import (changes to "Stop") |
| Select All | Always | N/A |
| Select None | Always | N/A |

### Progress Indicators

- Determinate progress bar (0-100%)
- Current operation text
- Item count (e.g., "45 of 145")
- Estimated time remaining (future)

## User Feedback Mechanisms

1. **Visual Progress**: Animated progress bar
2. **Text Updates**: Current operation description
3. **Sound**: System beep on completion (optional)
4. **Taskbar**: Windows taskbar progress (future)
5. **Notifications**: Windows notification on completion (future)

---

**Document Purpose**: UI/UX reference for developers and designers
**Related**: See PROFILE_DATA_COPY_DESIGN.md for technical implementation
