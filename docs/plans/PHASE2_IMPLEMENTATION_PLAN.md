# Phase 2: Implementation Plan - Enhanced FabricationSample with Export Capabilities

## Executive Summary

This document provides a comprehensive plan to combine the best features of DiscordCADmep (CSV export + NETLOAD commands) and FabricationSample (comprehensive WPF UI + full CRUD) into an enhanced unified application.

---

## 1. FEATURE MATRIX

### From DiscordCADmep (Export Features)

| Feature | Command | Status | Integration Target |
|---------|---------|--------|-------------------|
| **Item Data Export** | `ExportItemData` | Keep | Add to Commands + UI Button |
| **Price Tables Export** | `GetPriceTables` | Keep | Add to Commands + UI Button |
| **Product Info Export** | `GetProductInfo` | Keep | Add to Commands + UI Button |
| **Installation Times Export** | `GetInstallationTimes` | Keep | Add to Commands + UI Button |
| **Item Installation Tables** | `GetItemInstallationTables` | Keep | Add to Commands + UI Button |
| **Item Labor Calculation** | `GetItemLabor` | Keep | Add to Commands + UI Button |
| **CSV Helper Methods** | `StringExtensions.WrapForCSV()` | Keep | Move to Utilities namespace |
| **Folder Browser Pattern** | FolderBrowserDialog usage | Keep | Standardize across exports |
| **Timestamp Naming** | `yyyyMMdd_HHmmss` format | Keep | Use for all exports |

### From FabricationSample (UI/CRUD Features)

| Feature | Status | Notes |
|---------|--------|-------|
| **Database Editor** | Keep | Core UI - enhance with export buttons |
| **Item Editor** | Keep | Core UI - add export capabilities |
| **Service Editor** | Keep | Keep as-is |
| **Price List Management** | Keep | Add export button |
| **Installation Times Management** | Keep | Add export button |
| **Fabrication Times Management** | Keep | Add export button |
| **Product Database Management** | Keep | Add export button |
| **Material/Gauge/Specification CRUD** | Keep | Keep as-is |
| **Custom Data Management** | Keep | Keep as-is |
| **Service Template Management** | Keep | Add item export from services |
| **Product List Import (CSV)** | Enhance | Currently partial - expand |

### New Features to Add

| Feature | Priority | Description |
|---------|----------|-------------|
| **CSV Import Infrastructure** | High | Full bidirectional CSV import for all data types |
| **Export Progress Indicators** | Medium | Show progress during large exports |
| **Export Configuration** | Medium | Save/load export preferences |
| **Batch Import/Export** | Low | Multi-file operations |
| **Export Templates** | Low | Custom CSV column configurations |

---

## 2. APPLICATION ARCHITECTURE DESIGN

### 2.1 Module Structure

```
FabricationSample/
├── Commands/                          [NEW]
│   ├── ExportCommands.cs             (NETLOAD commands)
│   └── UICommands.cs                 (Existing FabAPI command)
├── Core/
│   ├── Sample.cs                     [EXISTING - MODIFY]
│   ├── FabricationWindow.xaml/cs     [EXISTING - MODIFY]
│   └── FabricationManager.cs         [EXISTING - KEEP]
├── Data/
│   ├── DataMapping.cs                [EXISTING - KEEP]
│   └── ExportModels.cs               [NEW]
├── Services/                          [NEW]
│   ├── Export/
│   │   ├── IExportService.cs         (Interface)
│   │   ├── CsvExportService.cs       (CSV export logic)
│   │   ├── ItemExportService.cs      (Item-specific exports)
│   │   ├── PriceExportService.cs     (Price table exports)
│   │   └── InstallExportService.cs   (Installation time exports)
│   └── Import/
│       ├── IImportService.cs         (Interface)
│       ├── CsvImportService.cs       (CSV import logic)
│       └── ValidationService.cs      (Import validation)
├── UserControls/
│   ├── DatabaseEditor/
│   │   ├── DatabaseEditor.xaml/cs    [EXISTING - MODIFY]
│   │   ├── DatabaseEditor-*.cs       [EXISTING - MODIFY]
│   │   └── ExportPanel.xaml/cs       [NEW]
│   ├── ItemEditor/
│   │   └── ItemEditor.xaml/cs        [EXISTING - MODIFY]
│   └── ServiceEditor/
│       └── ServiceEditor.xaml/cs     [EXISTING - KEEP]
├── Utilities/                         [NEW]
│   ├── CsvHelpers.cs                 (CSV formatting/parsing)
│   ├── FileHelpers.cs                (File I/O operations)
│   └── ProgressReporter.cs           (Progress tracking)
└── Windows/                           [EXISTING - ADD NEW]
    └── ExportConfigWindow.xaml/cs    [NEW]
```

### 2.2 Separation of Concerns

#### Layer 1: Commands (NETLOAD Entry Points)
- **Purpose**: Quick-access commands for experienced users
- **Pattern**: Static methods with `[CommandMethod]` attribute
- **Responsibility**: Parameter validation, user prompts, call services
- **Error Handling**: Try-catch with user-friendly messages

#### Layer 2: Services (Business Logic)
- **Purpose**: Reusable export/import logic
- **Pattern**: Interface-based with dependency injection ready
- **Responsibility**: Data access, transformation, CSV generation
- **Error Handling**: ResultStatus pattern from FabricationAPI

#### Layer 3: UI (WPF Dialogs and Controls)
- **Purpose**: Guided workflows for less experienced users
- **Pattern**: MVVM-lite (code-behind acceptable for simplicity)
- **Responsibility**: User input, progress display, call services
- **Error Handling**: MessageBox with detailed error info

#### Layer 4: Utilities (Shared Helpers)
- **Purpose**: Cross-cutting concerns
- **Pattern**: Static utility classes
- **Responsibility**: CSV formatting, file operations, validation
- **Error Handling**: Throw exceptions with clear messages

### 2.3 Data Flow

```
User Action (NETLOAD or UI Button)
    ↓
Command/Event Handler
    ↓
Export Service
    ↓
    ├─→ Database Access (via Autodesk.Fabrication.DB)
    ├─→ Data Transformation
    ├─→ CSV Generation (via Utilities.CsvHelpers)
    └─→ File Writing (via Utilities.FileHelpers)
    ↓
Result returned to user (file opened or error shown)
```

---

## 3. UI/UX PLAN FOR IMPORT/EXPORT

### 3.1 DatabaseEditor Enhancements

#### New Export Tab (tbiExport)
Add a dedicated export tab with organized sections:

```
┌─ Export Operations ───────────────────────────────────┐
│                                                        │
│  [Items & Services]                                    │
│    ○ Export Item Data              [Export] [Config]  │
│    ○ Export Item Labor              [Export] [Config]  │
│    ○ Export Item Installation Tables [Export] [Config]│
│                                                        │
│  [Pricing & Costs]                                     │
│    ○ Export Price Tables            [Export] [Config]  │
│    ○ Export Product Info (All-in-One) [Export]        │
│                                                        │
│  [Labor & Times]                                       │
│    ○ Export Installation Times      [Export] [Config]  │
│    ○ Export Fabrication Times       [Export] [Config]  │
│                                                        │
│  [Quick Actions]                                       │
│    ☐ Open file after export                           │
│    ☐ Export to timestamped folder                     │
│    Default folder: [C:\Exports        ] [Browse...]    │
│                                                        │
│  [Progress]                                            │
│    ▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░ 50% (2500/5000 products)      │
│                                                        │
└────────────────────────────────────────────────────────┘
```

#### Enhanced Price List Tab
Add export button next to "Update Prices":
- **Button**: "Export Current Price List"
- **Function**: Export just the selected price list to CSV
- **Shortcut**: Right-click context menu on price list grid

#### Enhanced Installation Times Tab
Add export button next to table selector:
- **Button**: "Export Current Table"
- **Function**: Export just the selected installation times table
- **Shortcut**: Right-click context menu on table grid

### 3.2 ItemEditor Enhancements

#### Product List Section
Add import/export buttons in the product list panel:

```
┌─ Product List ─────────────────────────────────┐
│                                                 │
│  [Import CSV] [Export CSV] [Create from File]  │
│                                                 │
│  ┌─────────────────────────────────────────┐  │
│  │ Name    │ Dim1 │ Dim2 │ Weight │ ID │...│  │
│  ├─────────────────────────────────────────┤  │
│  │ 12x12   │ 12   │ 12   │ 5.2    │ ABC│...│  │
│  │ 12x6    │ 12   │ 6    │ 3.1    │ DEF│...│  │
│  └─────────────────────────────────────────┘  │
│                                                 │
└─────────────────────────────────────────────────┘
```

**Import CSV Workflow:**
1. User clicks "Import CSV"
2. File dialog opens (CSV only)
3. CSV parser validates header structure
4. Preview dialog shows first 10 rows
5. User confirms import
6. Data applied to product list
7. Success message with row count

**Export CSV Workflow:**
1. User clicks "Export CSV"
2. File save dialog opens (default: ItemName_ProductList.csv)
3. Current product list exported with headers
4. File opened in default CSV viewer
5. Success notification

### 3.3 Service Editor Enhancements

Add export button in service template view:
- **Button**: "Export Service Items"
- **Function**: Export all items from selected service/template
- **Output**: CSV with service hierarchy context

### 3.4 New Export Configuration Window

```
┌─ Export Configuration ─────────────────────────┐
│                                                 │
│  Export Type: [Product Info ▼]                 │
│                                                 │
│  Column Selection:                              │
│    ☑ Product ID                                 │
│    ☑ Product Name                               │
│    ☑ Manufacturer                               │
│    ☑ Price Data                                 │
│    ☑ Installation Times                         │
│    ☐ Supplier IDs (Select suppliers...)         │
│    ☐ Custom Fields                              │
│                                                 │
│  Filtering:                                     │
│    ○ All Products                               │
│    ○ Product Listed Only                        │
│    ○ Service: [Select Service ▼]               │
│    ○ Product Group: [Select Group ▼]           │
│                                                 │
│  Options:                                       │
│    ☑ Include header row                         │
│    ☐ Exclude N/A values                         │
│    ☐ Quote all fields                           │
│    CSV Delimiter: [, (comma) ▼]                │
│                                                 │
│  [Save as Template]  [Load Template]            │
│                                                 │
│  [Cancel]                           [Export]    │
│                                                 │
└─────────────────────────────────────────────────┘
```

---

## 4. NETLOAD COMMAND STRUCTURE

### 4.1 Command Naming Conventions

**Pattern**: `<Action><DataType>`

**Actions**:
- `Export` - Write data to CSV
- `Import` - Read data from CSV
- `Show` - Open UI window

**Examples**:
- `ExportItemData`
- `ExportPriceTables`
- `ImportProductList`
- `ShowExportDialog`

### 4.2 Complete Command List

#### Quick Export Commands (from DiscordCADmep)
```csharp
[CommandMethod("ExportItemData")]
[CommandMethod("ExportPriceTables")]
[CommandMethod("ExportProductInfo")]
[CommandMethod("ExportInstallationTimes")]
[CommandMethod("ExportItemLabor")]
[CommandMethod("ExportItemInstallTables")]
```

#### New Import Commands
```csharp
[CommandMethod("ImportProductList")]
[CommandMethod("ImportPriceList")]
[CommandMethod("ImportInstallationTimes")]
```

#### UI Launch Commands
```csharp
[CommandMethod("FabAPI")]              // Existing - opens main UI
[CommandMethod("FabExport")]           // New - opens export dialog
[CommandMethod("FabImport")]           // New - opens import dialog
```

### 4.3 Command Implementation Pattern

```csharp
[CommandMethod("ExportProductInfo")]
public static void ExportProductInfo()
{
    try
    {
        // 1. Validate environment
        if (!ValidateFabricationLoaded())
        {
            ShowError("Fabrication API not loaded");
            return;
        }

        // 2. Get export location
        string exportPath = PromptForExportLocation("Product Info");
        if (string.IsNullOrEmpty(exportPath))
            return; // User cancelled

        // 3. Call service layer
        var exportService = new ProductExportService();
        var result = exportService.ExportProductInfo(exportPath);

        // 4. Handle result
        if (result.Success)
        {
            if (MessageBox.Show($"Export complete: {result.FilePath}\n\nOpen file?",
                "Export Complete", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Process.Start("explorer.exe", result.FilePath);
            }
        }
        else
        {
            ShowError($"Export failed: {result.ErrorMessage}");
        }
    }
    catch (Exception ex)
    {
        ShowError($"Unexpected error: {ex.Message}");
        LogError("ExportProductInfo", ex);
    }
}
```

### 4.4 Coexistence with UI Approach

**Philosophy**: Commands and UI complement each other

**For Power Users** (NETLOAD Commands):
- Fast, keyboard-driven workflow
- No dialogs (except file selection and results)
- Consistent output locations
- Scriptable for automation

**For General Users** (UI Dialogs):
- Guided workflows with previews
- Configuration options exposed
- Help text and tooltips
- Visual progress indication

**Shared Backend**:
- Both approaches call the same service layer
- Consistent export formats
- Same validation logic
- Common error handling

---

## 5. CSV SCHEMA DEFINITIONS

### 5.1 Item Data Export (ExportItemData)

**File**: `ItemReport_YYYYMMDD_HHMMSS.csv`

| Column | Data Type | Description | Validation |
|--------|-----------|-------------|------------|
| ServiceName | String | Service name | Required |
| ServiceTemplate | String | Template name | Required |
| ButtonName | String | Service button name | Required |
| ItemFilePath | String | Relative item path | Required |
| ProductListEntryName | String | Product entry name | Optional (N/A if none) |
| ConditionDescription | String | Template condition desc | Optional |
| GreaterThan | String | Condition min value | Unrestricted or numeric |
| Id | String | Condition ID | N/A or integer |
| LessThanEqualTo | String | Condition max value | Unrestricted or numeric |

**Example Row**:
```csv
"HVAC","Standard Template","Duct","Ducting\Rect\Straight.itm","12x12","Size >= 12","12","42","24"
```

### 5.2 Price Tables Export (GetPriceTables)

#### Main File: `PriceLists.csv`

| Column | Data Type | Description | Validation |
|--------|-----------|-------------|------------|
| SupplierGroup | String | Supplier group name | Required |
| PriceListName | String | Price list name | Required |
| Id | String | Database product ID | Required |
| Cost | Decimal | Product cost | >= 0 |
| Discount | String | Discount code | Optional |
| Units | String | "per(ft)" or "(each)" | Required |
| Date | String | "DD/MM/YYYY" | Valid date or "None" |
| Status | String | "Active", "POA", "Discon" | Required |

#### Breakpoint Files: `PriceBreakPoints_[ListName].csv`

**Metadata Rows** (first 9 rows):
```csv
"SupplierGroup","[GroupName]"
"PriceListName","[ListName]"
"CostedBy","[Area/Weight/etc]"
"HorizontalUnits","[Units]"
"HorizontalBreakPointType","[Type]"
"HorizontalCompareBy","[CompareType]"
"VerticalBreakPointType","[Type]"
"VerticalCompareBy","[CompareType]"
"VerticalUnits","[Units]"
```

**Data Rows** (row 10+):
First column = Vertical breakpoint value
Remaining columns = Horizontal breakpoint values (header) and prices (data)

### 5.3 Product Info Export (GetProductInfo)

**File**: `ProductInfo_YYYYMMDD_HHMMSS.csv`

**Column Groups**:
1. **Product Definition** (columns 1-15)
   - Id, IsProductListed, ProductGroup, Manufacturer, ProductName, Description, Size, Material, Specification, InstallType, Source, Range, Finish, (skip), Id

2. **Supplier IDs** (dynamic columns)
   - One column per supplier with external product IDs

3. **Price Data** (repeated per price list, columns variable)
   - (skip), SupplierGroup, PriceListName, Id, Cost, Discount, Units, Date, Status

4. **Installation Times** (repeated per install table, columns variable)
   - (skip), InstallTableName, InstallId, LaborRate, LaborUnits, LaborStatus

**Multi-row Structure**:
- Product definition appears only in first row per product
- Additional rows contain multiple price lists or installation times
- Empty cells (N/A) maintain column alignment

**Note**: "(skip)" columns are placeholders for Excel/import parsing

### 5.4 Installation Times Export (GetInstallationTimes)

#### Main File: `InstallationProducts.csv`

| Column | Data Type | Description | Validation |
|--------|-----------|-------------|------------|
| TableName | String | Installation table name | Required |
| TableGroup | String | Table group | Required |
| TableType | String | "Simple" or breakpoint type | Required |
| TableClass | String | API class name | Reference only |
| Id | String | Database product ID | Required |
| LaborRate | Decimal | Labor time value | >= 0 |
| Units | String | "per(ft)" or "(each)" | Required |
| Status | String | "Active", "POA", "Discon" | Required |

#### Breakpoint Files: `InstallBreakPoints_[Group]_[Name].csv`

Similar structure to Price breakpoint files (metadata + data grid)

### 5.5 Item Labor Export (GetItemLabor)

**File**: `ItemLabor_YYYYMMDD_HHMMSS.csv`

| Column | Data Type | Description | Validation |
|--------|-----------|-------------|------------|
| ServiceName | String | Service name | Required |
| ButtonName | String | Button name | Required |
| ItemPath | String | Item file path | Required |
| DatabaseId | String | Product database ID | Required |
| Dim1Value | Decimal | Primary dimension | >= 0 |
| Dim2Value | Decimal | Secondary dimension | >= 0 |
| InstallTableName | String | Installation table | Required |
| TableType | String | "1D" or "2D" | Required |
| LaborValue | Decimal | Calculated labor | >= 0 or N/A |
| LookupMethod | String | Lookup details/debug | Reference |

**LookupMethod Values**:
- `V={value}` - 1D vertical lookup
- `V={v},H={h}` - 2D matrix lookup
- `No install table` - Item has no table assigned
- `Dim1 is 0` - Invalid dimension
- `BP index not found` - No matching breakpoint

### 5.6 Product List CSV (Import/Export)

**File**: `[ItemName]_ProductList.csv`

**Header Format**:
- Standard columns: `Name`, `Weight`, `Id`
- Dimension columns: `DIM:[DimensionName]`
- Option columns: `OPT:[OptionName]`

**Example**:
```csv
Name,DIM:Diameter,DIM:Length,Weight,Id
12x12,12,24,5.2,ABC123
12x6,12,12,3.1,DEF456
```

**Validation Rules**:
- Name must be unique within product list
- Dimension names must match item dimensions
- Option names must match item options
- Weight must be numeric >= 0
- Id should be valid database ID (optional)

---

## 6. IMPLEMENTATION ROADMAP

### Phase 1: Foundation (Week 1-2)

**Priority**: Critical Infrastructure

#### Tasks:
1. **Create Service Layer Structure**
   - [ ] Create `Services/Export/` folder structure
   - [ ] Create `Services/Import/` folder structure
   - [ ] Define `IExportService` interface
   - [ ] Define `IImportService` interface

2. **Move CSV Utilities**
   - [ ] Create `Utilities/CsvHelpers.cs`
   - [ ] Port `StringExtensions` from DiscordCADmep
   - [ ] Add CSV parsing methods
   - [ ] Add validation helpers

3. **Create Commands Infrastructure**
   - [ ] Create `Commands/ExportCommands.cs`
   - [ ] Add command registration to `Sample.cs`
   - [ ] Implement validation helpers
   - [ ] Implement error handling patterns

**Deliverable**: Infrastructure ready for feature implementation

**Dependencies**: None

### Phase 2: Core Export Features (Week 3-4)

**Priority**: High - Bring DiscordCADmep functionality to FabricationSample

#### Tasks:
1. **Port Export Commands**
   - [ ] Port `ExportItemData` to ExportCommands.cs
   - [ ] Port `GetPriceTables` to ExportCommands.cs
   - [ ] Port `GetProductInfo` to ExportCommands.cs
   - [ ] Port `GetInstallationTimes` to ExportCommands.cs
   - [ ] Port `GetItemLabor` to ExportCommands.cs

2. **Create Export Services**
   - [ ] Implement `ItemExportService.cs`
   - [ ] Implement `PriceExportService.cs`
   - [ ] Implement `InstallExportService.cs`
   - [ ] Implement `ProductExportService.cs`

3. **Refactor Commands to Use Services**
   - [ ] Refactor each command to call service layer
   - [ ] Remove direct data access from commands
   - [ ] Add progress reporting
   - [ ] Add cancellation support

**Deliverable**: All DiscordCADmep export commands working in FabricationSample

**Dependencies**: Phase 1 complete

### Phase 3: UI Integration (Week 5-6)

**Priority**: High - Make exports accessible from UI

#### Tasks:
1. **Add Export Tab to DatabaseEditor**
   - [ ] Design Export tab XAML layout
   - [ ] Add export button handlers
   - [ ] Implement progress indicators
   - [ ] Add configuration options

2. **Enhance PriceList Tab**
   - [ ] Add "Export Current Price List" button
   - [ ] Implement single table export
   - [ ] Add right-click context menu

3. **Enhance Installation Times Tab**
   - [ ] Add "Export Current Table" button
   - [ ] Implement single table export
   - [ ] Add right-click context menu

4. **Enhance ItemEditor Product List**
   - [ ] Add "Export CSV" button
   - [ ] Implement product list export
   - [ ] Add export preview dialog

**Deliverable**: All exports accessible from UI

**Dependencies**: Phase 2 complete

### Phase 4: Import Features (Week 7-8)

**Priority**: Medium - Enable data round-tripping

#### Tasks:
1. **Create Import Services**
   - [ ] Implement `CsvImportService.cs` base class
   - [ ] Implement `ValidationService.cs`
   - [ ] Add CSV parsing logic
   - [ ] Add validation rules engine

2. **Implement Product List Import**
   - [ ] Enhance existing CSV import in ItemEditor
   - [ ] Add validation and preview
   - [ ] Add error reporting
   - [ ] Test with various CSV formats

3. **Add Import Commands**
   - [ ] Implement `ImportProductList` command
   - [ ] Implement `ImportPriceList` command (basic)
   - [ ] Add import validation
   - [ ] Add rollback on error

4. **Create Import UI**
   - [ ] Design import preview dialog
   - [ ] Add column mapping UI
   - [ ] Add validation error display
   - [ ] Add confirmation step

**Deliverable**: Product list import fully functional

**Dependencies**: Phase 3 complete

### Phase 5: Advanced Features (Week 9-10)

**Priority**: Medium - Polish and power user features

#### Tasks:
1. **Export Configuration**
   - [ ] Design `ExportConfigWindow.xaml`
   - [ ] Implement configuration persistence
   - [ ] Add template save/load
   - [ ] Add column selection

2. **Batch Operations**
   - [ ] Add multi-file export capability
   - [ ] Add progress tracking for batch ops
   - [ ] Add batch import with validation

3. **Export Templates**
   - [ ] Design template format (JSON)
   - [ ] Implement template loading
   - [ ] Add predefined templates
   - [ ] Add custom template editor

**Deliverable**: Advanced export capabilities

**Dependencies**: Phase 4 complete

### Phase 6: Testing & Documentation (Week 11-12)

**Priority**: High - Ensure quality and usability

#### Tasks:
1. **Testing**
   - [ ] Unit tests for CSV helpers
   - [ ] Integration tests for export services
   - [ ] End-to-end tests for commands
   - [ ] UI automation tests for dialogs

2. **Documentation**
   - [ ] Update README.md with new features
   - [ ] Create export/import tutorials
   - [ ] Document CSV schemas
   - [ ] Create command reference

3. **Bug Fixes**
   - [ ] Address reported issues
   - [ ] Performance optimization
   - [ ] Memory leak testing
   - [ ] Large dataset testing

**Deliverable**: Production-ready application

**Dependencies**: Phases 1-5 complete

### Optional Phase 7: Future Enhancements

**Priority**: Low - Nice-to-have features

#### Potential Tasks:
- [ ] Excel export (XLSX format)
- [ ] JSON export for API integration
- [ ] Scheduled/automated exports
- [ ] Cloud storage integration
- [ ] Export analytics dashboard
- [ ] Multi-language support
- [ ] Custom export plugins

---

## 7. FILE STRUCTURE DETAILS

### 7.1 New Files to Create

```
Commands/
├── ExportCommands.cs          (250 lines) - All export NETLOAD commands
└── ImportCommands.cs          (100 lines) - All import NETLOAD commands

Services/
├── Export/
│   ├── IExportService.cs      (30 lines)  - Export service interface
│   ├── CsvExportService.cs    (150 lines) - Base CSV export logic
│   ├── ItemExportService.cs   (200 lines) - Item/service exports
│   ├── PriceExportService.cs  (250 lines) - Price table exports
│   └── InstallExportService.cs(300 lines) - Installation time exports
└── Import/
    ├── IImportService.cs      (30 lines)  - Import service interface
    ├── CsvImportService.cs    (200 lines) - Base CSV import logic
    └── ValidationService.cs   (150 lines) - Import validation

Utilities/
├── CsvHelpers.cs              (150 lines) - CSV formatting/parsing
├── FileHelpers.cs             (100 lines) - File I/O operations
└── ProgressReporter.cs        (80 lines)  - Progress tracking

Data/
└── ExportModels.cs            (100 lines) - Export result models

UserControls/DatabaseEditor/
└── ExportPanel.xaml/cs        (400 lines) - Export UI tab

Windows/
├── ExportConfigWindow.xaml/cs (300 lines) - Export configuration
└── ImportPreviewWindow.xaml/cs(250 lines) - Import preview dialog
```

**Total New Code**: ~2,940 lines across 18 files

### 7.2 Existing Files to Modify

#### Sample.cs
**Changes**:
- Add initialization of export services
- Register new NETLOAD commands
- Add error logging infrastructure

**Estimated Impact**: +50 lines

#### FabricationWindow.xaml/cs
**Changes**:
- No changes required (uses ContentControl binding)

**Estimated Impact**: 0 lines

#### DatabaseEditor.xaml
**Changes**:
- Add new TabItem for Export tab
- Add export buttons to existing tabs

**Estimated Impact**: +100 lines XAML

#### DatabaseEditor.xaml.cs
**Changes**:
- Add export button click handlers
- Add progress tracking integration

**Estimated Impact**: +150 lines

#### DatabaseEditor-PriceLists.cs
**Changes**:
- Add export current price list method
- Add context menu export option

**Estimated Impact**: +80 lines

#### DatabaseEditor-InstallTimes.cs
**Changes**:
- Add export current table method
- Add context menu export option

**Estimated Impact**: +80 lines

#### ItemEditor.xaml
**Changes**:
- Add import/export buttons to product list panel

**Estimated Impact**: +50 lines XAML

#### ItemEditor.xaml.cs
**Changes**:
- Enhance `LoadProductListData()` method
- Add export product list method
- Add validation for imports

**Estimated Impact**: +200 lines

**Total Modified Code**: ~710 lines across 8 files

### 7.3 Folder Organization

```
FabricationSample/
├── Commands/                    [NEW - NETLOAD entry points]
├── Core/                        [RENAME from root]
│   ├── Sample.cs
│   ├── FabricationWindow.xaml/cs
│   └── ...
├── Data/                        [EXISTING - add export models]
├── Examples/                    [EXISTING - keep as reference]
├── Manager/                     [EXISTING - no changes]
├── Services/                    [NEW - business logic layer]
│   ├── Export/
│   └── Import/
├── UserControls/                [EXISTING - modify several]
│   ├── DatabaseEditor/
│   ├── ItemEditor/
│   └── ServiceEditor/
├── Utilities/                   [NEW - shared helpers]
├── Windows/                     [EXISTING - add new dialogs]
└── Resources/                   [EXISTING - no changes]
```

**Rationale for Organization**:
1. **Commands** - Separate from core, clear NETLOAD entry point
2. **Services** - Clean business logic layer, testable
3. **Utilities** - Shared code, no dependencies
4. **Core** - Keep UI initialization separate from features

---

## 8. TECHNICAL SPECIFICATIONS

### 8.1 CSV Helper Implementation

```csharp
namespace FabricationSample.Utilities
{
    /// <summary>
    /// CSV formatting and parsing utilities
    /// </summary>
    public static class CsvHelpers
    {
        /// <summary>
        /// Wrap a single value for CSV output (quotes and escapes)
        /// </summary>
        public static string WrapForCsv(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"N/A\"";

            // Escape quotes by doubling them (CSV standard)
            string escaped = value.Replace("\"", "\"\"");

            // Always quote to handle commas, newlines, quotes
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Wrap multiple values for CSV output (joins with commas)
        /// </summary>
        public static string WrapForCsv(params object[] values)
        {
            if (values == null || values.Length == 0)
                return "\"N/A\"";

            return string.Join(",", values.Select(v =>
                (v?.ToString() ?? "N/A").WrapForCsv()));
        }

        /// <summary>
        /// Parse a CSV line into fields (handles quoted values)
        /// </summary>
        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentField.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // Field separator
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add final field
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// Validate CSV header matches expected columns
        /// </summary>
        public static ValidationResult ValidateHeader(
            List<string> actualHeader,
            List<string> expectedHeader)
        {
            var result = new ValidationResult { IsValid = true };

            // Check column count
            if (actualHeader.Count != expectedHeader.Count)
            {
                result.IsValid = false;
                result.Errors.Add($"Expected {expectedHeader.Count} columns, found {actualHeader.Count}");
            }

            // Check column names
            for (int i = 0; i < Math.Min(actualHeader.Count, expectedHeader.Count); i++)
            {
                if (!actualHeader[i].Equals(expectedHeader[i], StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Column {i + 1}: Expected '{expectedHeader[i]}', found '{actualHeader[i]}'");
                }
            }

            return result;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
```

### 8.2 Export Service Base Class

```csharp
namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Base interface for all export services
    /// </summary>
    public interface IExportService
    {
        ExportResult Export(string outputPath, ExportOptions options = null);
        void Cancel();
        event EventHandler<ProgressEventArgs> ProgressChanged;
    }

    /// <summary>
    /// Base class for CSV export operations
    /// </summary>
    public abstract class CsvExportService : IExportService
    {
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        private bool _cancelled = false;

        /// <summary>
        /// Export data to CSV file
        /// </summary>
        public ExportResult Export(string outputPath, ExportOptions options = null)
        {
            try
            {
                _cancelled = false;
                options = options ?? new ExportOptions();

                // Create directory if needed
                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Generate CSV data
                var csvData = GenerateCsvData(options);

                if (_cancelled)
                    return ExportResult.Cancelled();

                // Write to file
                File.WriteAllLines(outputPath, csvData);

                return ExportResult.Success(outputPath);
            }
            catch (Exception ex)
            {
                return ExportResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Cancel ongoing export
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        /// <summary>
        /// Override to implement specific export logic
        /// </summary>
        protected abstract List<string> GenerateCsvData(ExportOptions options);

        /// <summary>
        /// Report progress to listeners
        /// </summary>
        protected void ReportProgress(int current, int total, string message)
        {
            if (_cancelled) return;

            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message,
                Percentage = total > 0 ? (int)((current / (double)total) * 100) : 0
            });
        }

        /// <summary>
        /// Check if operation was cancelled
        /// </summary>
        protected bool IsCancelled => _cancelled;
    }

    /// <summary>
    /// Export operation result
    /// </summary>
    public class ExportResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string ErrorMessage { get; set; }
        public bool WasCancelled { get; set; }
        public int RowCount { get; set; }

        public static ExportResult Success(string filePath, int rowCount = 0)
        {
            return new ExportResult
            {
                Success = true,
                FilePath = filePath,
                RowCount = rowCount
            };
        }

        public static ExportResult Failure(string errorMessage)
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        public static ExportResult Cancelled()
        {
            return new ExportResult
            {
                Success = false,
                WasCancelled = true
            };
        }
    }

    /// <summary>
    /// Export configuration options
    /// </summary>
    public class ExportOptions
    {
        public bool IncludeHeader { get; set; } = true;
        public bool ExcludeNullValues { get; set; } = false;
        public bool OpenAfterExport { get; set; } = true;
        public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Progress reporting event args
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public int Percentage { get; set; }
        public string Message { get; set; }
    }
}
```

### 8.3 Command Helper Pattern

```csharp
namespace FabricationSample.Commands
{
    /// <summary>
    /// Helper methods for NETLOAD commands
    /// </summary>
    internal static class CommandHelpers
    {
        /// <summary>
        /// Validate that Fabrication API is loaded
        /// </summary>
        public static bool ValidateFabricationLoaded()
        {
            try
            {
                // Try to access fabrication database
                var services = Autodesk.Fabrication.DB.Database.Services;
                return true;
            }
            catch
            {
                MessageBox.Show(
                    "Fabrication API is not loaded.\n\nPlease load CADmep and open a valid fabrication job.",
                    "Fabrication API Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Prompt user for export location with folder browser
        /// </summary>
        public static string PromptForExportLocation(string exportType)
        {
            try
            {
                string defaultFolder = GetDefaultExportFolder();

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = $"Select output folder for {exportType}";
                    dialog.ShowNewFolderButton = true;
                    dialog.SelectedPath = defaultFolder;

                    if (dialog.ShowDialog() == DialogResult.OK)
                        return dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error selecting folder: {ex.Message}");
            }

            return null; // User cancelled
        }

        /// <summary>
        /// Get default export folder (from config or working directory)
        /// </summary>
        private static string GetDefaultExportFolder()
        {
            try
            {
                string workingDir = Autodesk.Fabrication.ApplicationServices.Application.WorkingDirectory;
                string parentDir = Path.GetDirectoryName(workingDir);
                return Path.Combine(parentDir, "Exports");
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>
        /// Generate timestamped filename
        /// </summary>
        public static string GenerateTimestampedFilename(string baseName, string extension = ".csv")
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"{baseName}_{timestamp}{extension}";
        }

        /// <summary>
        /// Show error message to user
        /// </summary>
        public static void ShowError(string message)
        {
            MessageBox.Show(message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Princ($"ERROR: {message}");
        }

        /// <summary>
        /// Show success message and optionally open file
        /// </summary>
        public static void ShowSuccess(string filePath, int rowCount = 0)
        {
            string message = rowCount > 0
                ? $"Export complete: {filePath}\n\n{rowCount} rows exported.\n\nOpen file?"
                : $"Export complete: {filePath}\n\nOpen file?";

            if (MessageBox.Show(message, "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    Process.Start("explorer.exe", filePath);
                }
                catch (Exception ex)
                {
                    ShowError($"Could not open file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Write message to AutoCAD command line
        /// </summary>
        public static void Princ(string message)
        {
            try
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n{message}");
            }
            catch
            {
                // Silently fail if command line not available
            }
        }
    }
}
```

---

## 9. RISK ASSESSMENT & MITIGATION

### High Risk Items

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Breaking existing functionality** | High | Medium | - Comprehensive testing of existing features<br>- Use existing partial classes pattern<br>- Keep modifications minimal |
| **Performance degradation on large datasets** | High | Medium | - Implement progress reporting<br>- Add cancellation support<br>- Use streaming for large files<br>- Batch processing where possible |
| **CSV format inconsistencies** | Medium | High | - Define strict schemas<br>- Implement validation<br>- Add format version to exports<br>- Comprehensive testing with real data |

### Medium Risk Items

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **UI complexity overwhelming users** | Medium | Medium | - Progressive disclosure design<br>- Sensible defaults<br>- Help tooltips<br>- Tutorial documentation |
| **Import validation missing edge cases** | Medium | High | - Extensive test data sets<br>- Validation rule engine<br>- Clear error messages<br>- Preview before commit |
| **Command name conflicts** | Low | Low | - Use unique prefixes (Fab*)<br>- Check for conflicts<br>- Document all commands |

---

## 10. SUCCESS CRITERIA

### Phase 2 Completion Criteria

**Must Have**:
- [ ] All 6 export commands from DiscordCADmep working in FabricationSample
- [ ] Export buttons accessible from DatabaseEditor UI
- [ ] CSV files match DiscordCADmep format exactly
- [ ] No regressions in existing FabricationSample functionality
- [ ] Basic documentation for new commands

**Should Have**:
- [ ] Product list CSV import enhanced and working
- [ ] Export progress indicators in UI
- [ ] Export configuration dialog functional
- [ ] Command help text available

**Nice to Have**:
- [ ] Batch export capabilities
- [ ] Export templates
- [ ] Advanced filtering options

### Quality Metrics

- **Code Coverage**: > 80% for new service layer code
- **Performance**: Export 5000 products in < 10 seconds
- **Usability**: Users can complete export in < 3 clicks
- **Stability**: Zero crashes during normal operations

---

## 11. NEXT STEPS

### Immediate Actions (Before Implementation)

1. **Review and Approval**
   - Review this plan with stakeholders
   - Confirm priorities and scope
   - Adjust timeline if needed

2. **Environment Setup**
   - Set up development branch in git
   - Configure build pipeline for testing
   - Set up test data environment

3. **Detailed Design**
   - Create mockups for new UI elements
   - Design export configuration schema
   - Define test cases

### Phase 1 Kickoff

1. **Create folder structure** (Commands/, Services/, Utilities/)
2. **Implement CsvHelpers utility class**
3. **Create ExportCommands.cs with command stubs**
4. **Set up unit testing framework**

---

## APPENDIX A: Reference Code Locations

### DiscordCADmep Key Code

| Feature | File | Lines |
|---------|------|-------|
| ExportItemData | INI.cs | 34-97 |
| GetPriceTables | INI.cs | 100-174 |
| GetProductInfo | INI.cs | 177-355 |
| GetInstallationTimes | INI.cs | 358-512 |
| GetItemLabor | INI.cs | 588-872 |
| StringExtensions | INI.cs | 884-889 |

### FabricationSample Key Code

| Feature | File | Lines |
|---------|------|-------|
| Main UI Window | FabricationWindow.xaml.cs | Full file |
| Database Editor | DatabaseEditor.xaml.cs | Full file |
| Price List UI | DatabaseEditor-PriceLists.cs | Full file |
| Installation Times UI | DatabaseEditor-InstallTimes.cs | Full file |
| Item Editor | ItemEditor.xaml.cs | Full file |
| Product List Import | ItemEditor.xaml.cs | 1665-1779 |
| Data Mapping | DataMapping.cs | Full file |

---

## APPENDIX B: Dependencies & Requirements

### Required Autodesk Components

- AutoCAD 2024 (or 2025 with version update)
- Fabrication CADmep 2024
- FabricationAPI.dll

### Required .NET Components

- .NET Framework 4.8
- System.Windows.Forms (for dialogs)
- System.Windows (WPF)
- System.Linq
- System.IO

### Development Tools

- Visual Studio 2019 or later
- Git for version control
- NUnit or MSTest for unit testing (optional but recommended)

---

## APPENDIX C: Glossary

| Term | Definition |
|------|------------|
| **NETLOAD** | AutoCAD command to load .NET assemblies at runtime |
| **CRUD** | Create, Read, Update, Delete operations |
| **WPF** | Windows Presentation Foundation (UI framework) |
| **ITM File** | Fabrication Item file format (.itm extension) |
| **Product List** | List of product configurations within an ITM file |
| **Service Template** | Reusable configuration for service definitions |
| **Breakpoint Table** | Table with dimension-based breakpoints for pricing/labor |
| **Database ID** | Unique identifier for products in the fabrication database |
| **Supplier Group** | Collection of price lists from a supplier |

---

**Document Version**: 1.0
**Date**: 2026-01-09
**Author**: Claude Opus 4.5
**Status**: Ready for Review and Implementation
