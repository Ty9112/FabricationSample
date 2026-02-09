# FabricationSample

Extended Autodesk Fabrication API Sample plugin for CADmep / ESTmep / CAMduct (Fabrication 2024).

> **This is a fork of [MartinMagallanes/FabricationSample](https://github.com/MartinMagallanes/FabricationSample)** with significant feature additions for production use. See [Summary of Extended Features](#summary-of-extended-features) below.

## Authors and Credits

This project builds on the original Autodesk Fabrication API Sample (shipped with the Fabrication SDK), updated for modern Fabrication versions and extended with production features for cross-configuration content management, data export/import, and profile management.

| Contributor | Role | GitHub |
|-------------|------|--------|
| **Autodesk** | Original author of the Fabrication API Sample application with WPF UI, database editor tabs, and AutoCAD plugin framework. Shipped as part of the Fabrication SDK. | - |
| **Martin Magallanes** | Updated the sample for post-2020 versions of AutoCAD, CADmep, and the Fabrication Suite. Original author of the NETLOAD export commands and DiscordCADmep. Made the project publicly accessible on GitHub. | [MartinMagallanes](https://github.com/MartinMagallanes) |
| **Tyler Phillips** | Extended the application with NETLOAD export/import commands, Content Transfer (cross-config ITM export/import with reference re-resolution), Profile Data Copy, CSV import services, Item Swap with undo, and UI enhancements. | [Ty9112](https://github.com/Ty9112) |

### Referenced Repositories

| Repository | Author | Description |
|------------|--------|-------------|
| [FabricationSample](https://github.com/MartinMagallanes/FabricationSample) | Martin Magallanes | Fabrication API sample updated for post-2020 Fabrication versions (upstream) |
| [DiscordCADmep](https://github.com/MartinMagallanes/DiscordCADmep) | Martin Magallanes | Simpler AutoCAD Fabrication plugin with NETLOAD export commands |
| [fabrication-api-xmldocs](https://github.com/Ty9112/fabrication-api-xmldocs) | DugganIS | XML documentation extracted from FabricationAPI.chm for IntelliSense support |

---


**Application:** Fabrication Sample (Extended)
**Fabrication Products:** ESTmep, CAMduct, CADmep
**Fabrication Version:** 2024
**Programming Language:** C#
**Type:** ExternalApplication
<br/>**Subject:** Extended Fabrication API sample with content transfer, profile management, data export/import, and item swap.
**Summary:** Builds on the original Autodesk Fabrication API Sample with cross-configuration content transfer (ITM export/import with reference re-resolution), profile data copy with selective cleanup and multi-profile push, CSV import/export services, and item swap with undo.

See the [Installation](#installation) section for setup instructions.


---

# Summary of Extended Features

The following features have been added to the base Fabrication Sample application by usage of ClaudeCode and additional personal tweaks and projects from the past.

## Table of Contents

- [Installation](#installation)
- [AutoCAD Commands (NETLOAD)](#autocad-commands-netload)
- [FabAPI UI Features](#fabapi-ui-features)
  - [Content Transfer (Export/Import Items)](#content-transfer-exportimport-items)
  - [Profile Data Copy](#profile-data-copy)
    - [Selective Cleanup](#selective-cleanup)
    - [Push to Profiles (Global Only)](#push-to-profiles-global-only)
  - [CSV Import (Product List, Price List)](#csv-import)
  - [Item Swap with Undo](#item-swap-with-undo)
- [Tabs Reference](#tabs-reference)
- [Project Structure](#project-structure)
- [Building from Source](#building-from-source)
- [Known Bugs](#known-bugs)

---

## Installation

### Prerequisites

- Autodesk Fabrication 2024 (CADmep, ESTmep, or CAMduct)
- AutoCAD 2024 (for CADmep NETLOAD commands)
- .NET Framework 4.8

### Install for CADmep (AutoCAD Plugin)

1. Build the solution or use the pre-compiled DLL from `bin\x64\Release\`
2. Copy the `ACAD\FabricationSample.bundle` folder to:
   ```
   C:\ProgramData\Autodesk\ApplicationPlugins\
   ```
3. Copy `FabricationSample.dll` from the build output into the bundle folder
4. A **"Fabrication Sample"** button will appear on the AutoCAD **Add-Ins** tab
5. Click it to open the FabAPI window, or type `FabAPI` at the AutoCAD command line

### Install for ESTmep / CAMduct (Fabrication Addin)

1. Copy `FabricationSample.dll` and `FabricationSample.addin` to:
   ```
   C:\ProgramData\Autodesk\Fabrication\Addins\2024\
   ```
2. Launch ESTmep or CAMduct — the addin loads automatically
3. The FabAPI window opens when the addin initializes

### NETLOAD Commands (CADmep Only)

For quick command-line access without the full FabAPI UI:

1. In AutoCAD, type `NETLOAD` at the command line
2. Browse to and select `FabricationSample.dll`
3. The commands listed below become available at the AutoCAD command prompt

---

## AutoCAD Commands (NETLOAD)

These commands are available after loading the DLL via `NETLOAD` or the AutoCAD plugin framework. Each exports fabrication data to CSV files.

### Export Commands

| Command | Description |
|---------|-------------|
| `FabAPI` | Opens the main FabAPI WPF window with all tabs and features |
| `GetProductInfo` | Exports product database entries with prices and labor rate values to CSV |
| `ExportItemData` | Exports service items with product list entries and dimensions to CSV |
| `GetPriceTables` | Exports price lists and breakpoint table data to CSV |
| `GetInstallationTimes` | Exports installation times tables with breakpoint values to CSV |
| `GetItemLabor` | Exports items with calculated labor values derived from breakpoint tables |
| `GetItemInstallationTables` | Exports items with their assigned installation times tables |
| `GetServiceTemplateData` | Exports service template data (prompts with a service selection dialog) |

### Import Commands

| Command | Description |
|---------|-------------|
| `ImportProductList` | Imports product list rows from a CSV file with column mapping |
| `ImportPriceList` | Imports price list data from a CSV file |
| `ImportProfileData` | Imports profile database files (.MAP files) from a source profile folder |

---

## FabAPI UI Features

The FabAPI window (`FabAPI` command) provides a tabbed interface for interacting with the Fabrication database. The following sections document the extended features added beyond the original Autodesk sample.

---

### Content Transfer (Export/Import Items)

**Location:** FabAPI Window > **Manage Content** tab > **Export Items** / **Import Items** buttons

This feature enables transferring `.itm` files (Fabrication item content) between different Fabrication configurations — for example, from a master/template configuration to a job-specific configuration.

#### The Problem It Solves

When you copy an `.itm` file from one Fabrication configuration to another, the internal database references (material, specification, price list, etc.) are stored by **index**, not by name. Index 5 in Config A might be "Copper Pipe" but index 5 in Config B might be "Steel Duct." After copying, the item's references point to the wrong things or to nothing at all.

Traditionally, fixing this requires manually re-setting product info, cost tables, and labor tables by hand using multiple tools (Product Information Editor, ESTmep, Ctrl+Shift+Right-Click bulk operations). Content Transfer automates this process.

#### Export Items

1. Open the FabAPI window and navigate to the **Manage Content** tab
2. Click **Export Items**
3. The Export Items window opens showing a tree view of all item folders with checkboxes
   - Expand folders to see individual `.itm` files
   - Check/uncheck folders to select/deselect all items within them
   - The "Selected: N items" counter updates as you make selections
4. Click **Browse** to choose an output folder
5. Click **Export**

**What gets exported:**
- Each selected `.itm` file is copied to the output folder
- Each companion `.png` thumbnail file is also copied (same name, different extension)
- A `manifest.json` file is generated containing:
  - Configuration name (source config identity)
  - Export timestamp and user info
  - Per-item metadata:
    - File name, source folder path, CID, Database ID
    - **Reference names**: Service, Material, Specification, Section, Price List, Supplier Group, Installation Times Table, Fabrication Times Table
    - Product list data (if applicable): row names, aliases, database IDs, order numbers, bought-out flags, weights

The manifest captures reference **names** (not indices), which enables name-based re-resolution during import.

#### Import Items

1. Open the FabAPI window in the **target** configuration and navigate to the **Manage Content** tab
2. Click **Import Items**
3. A folder browser opens — select the folder previously created by **Export Items** (the one containing `manifest.json`)
4. The Import Items preview window opens showing:
   - Source folder path and configuration name
   - Number of items in the package
   - Per-item details with reference validation status:
     - **Green "(ok)"** — the reference exists in the target database by name
     - **Yellow "(!) not found"** — the reference does not exist in the target database

5. **Reassigning Unmatched References:**
   - For any reference marked with a warning, a dropdown (ComboBox) appears next to it
   - The dropdown is populated with all available values from the **target** database
   - Select a replacement value, or leave it as "(skip - leave unresolved)" to import without fixing that reference
   - Available override types: Material, Specification, Section, Price List, Installation Times Table, Fabrication Times Table
   - Service is report-only (read-only on items and cannot be reassigned via the API)

6. **Select Target Folder:**
   - Use the target folder dropdown to choose which item folder the imported items will be placed into
   - The dropdown shows all item folders in a flattened hierarchy (e.g., "HVAC > Pipe > Copper")
   - Alternatively, click **"..."** to browse to a custom folder path

7. Check/uncheck items you want to import, then click **Import**

8. **Duplicate Database ID Check:**
   - Before importing, the system scans existing `.itm` files in the target folder
   - If any items in the package have Database IDs that already exist in the target folder, a warning dialog appears listing the conflicts
   - You can choose to proceed anyway or cancel

9. **Import Processing:**
   - Each selected `.itm` file is copied to the target folder
   - The companion `.png` file is also copied
   - Each item is loaded via the Fabrication API and references are re-resolved by name:
     - Material: matched by name, applied via `ChangeMaterial()`
     - Specification: matched by name, applied via `ChangeSpecification()`
     - Section: matched by description
     - Price List: matched by name across all supplier groups
     - Installation Times Table: matched by name
     - Fabrication Times Table: matched by name
   - If the user selected override values, those are used instead of the original reference names
   - The item is saved to persist the re-resolved references

10. **Results Dialog:**
    - Shows count of successfully imported items
    - Lists any warnings (unresolved references) and errors
    - The item folder tree automatically refreshes to show the newly imported items

---

### Profile Data Copy

**Location:** FabAPI Window > **Profiles** tab

Copies database files (`.MAP` files) between Fabrication profiles (e.g., from Global to a named profile, or between named profiles).

#### How to Use

1. Navigate to the **Profiles** tab
2. Select a **Source Profile** from the dropdown (includes "Global" and all named profiles)
3. Select a **Target Profile** (the profile you want to update)
4. Choose which **Data Types** to copy using the checkboxes:
   - Services, Materials, Specifications, Sections, Connectors, Seams
   - Cost tables, Fabrication times, Installation/Estimation times
   - Ancillaries, Dampers, Diameters, Suppliers, Layers
   - Setup, Air turns, and more
5. Click **Copy** to transfer the selected `.MAP` files
6. A backup of the target profile's files is created automatically before overwriting
7. Use **Restore Backup** to revert if needed

#### Selective Cleanup

For enumerable data types, click the data type name (underlined link) to open a **preview window** showing all items from the source. Uncheck items you don't want in the target profile. After the `.MAP` file is copied, a cleanup file is saved that automatically deletes the unwanted items when the target profile is loaded.

Supported data types for selective cleanup:
- **Price & Labor:** Suppliers, Costs / Price Lists, Installation Times
- **Primary:** Services, Fabrication Times, Materials, Specifications, Sections, Ancillaries
- **Secondary:** Connectors, Dampers, Stiffeners

#### Push to Profiles (Global Only)

When on the Global profile, the **Push to Profiles** panel appears. This lets you push selected data types from Global to multiple named profiles at once:

1. Select data types and configure selective items (same as single-copy)
2. Check one or more target profiles in the push panel
3. Click **Push to Profiles**
4. Each target receives the selected `.MAP` files, and if selective items were configured, a per-profile cleanup file (`_pending_cleanup.json`) is saved in each target's DATABASE folder
5. When a target profile is loaded in a future session, the cleanup runs automatically and shows a summary of deleted items

---

### CSV Import

#### Import Product List

**Command:** `ImportProductList` (NETLOAD) or via FabAPI UI

Imports product list rows from a CSV file into the Fabrication database.

1. Run the command or use the UI import function
2. Select a CSV file
3. A **Column Mapping** window appears — map your CSV columns to the expected fields:
   - Name, Alias, Database ID, Order Number, Bought Out, Weight
4. Preview the data to verify mappings
5. Confirm to import the rows into the target product list

#### Import Price List

**Command:** `ImportPriceList` (NETLOAD) or via FabAPI UI

Imports price list data from a CSV file.

1. Run the command
2. Select a CSV file and map columns
3. Preview and confirm the import

---

### Item Swap with Undo

Swap items in the current job with different items from the database, with full undo support.

1. Select items in the drawing
2. Use the swap functionality to replace them with items from a different service or pattern
3. If the result is not what you expected, use **Undo** to revert the swap
4. The undo manager tracks all swaps performed in the current session

---

## Tabs Reference

The FabAPI window contains the following tabs:

| Tab | Description |
|-----|-------------|
| Job Items | View and edit items in the current fabrication job |
| Product Database | Browse the product database entries |
| Price Lists | View and edit price lists and breakpoint tables |
| Supplier Discounts | Manage supplier discount codes and values |
| Fabrication Tables | View and edit fabrication times breakpoint tables |
| Installation Tables | View and edit installation times breakpoint tables |
| Job Custom Data | Manage job-level custom data fields |
| Item Custom Data | Manage item-level custom data fields |
| Item Statuses | Configure item status definitions |
| Job Statuses | Configure job status definitions |
| Service Types | View service type definitions |
| Point Locate | Point location data for job items |
| Services | Browse and inspect services and their templates |
| Service Templates | View and edit service template conditions |
| Materials | Browse materials and their gauges |
| Sections | Browse section definitions |
| Ancillaries | Browse ancillary items and their details |
| Job Information | View job properties and status history |
| **Manage Content** | **Item folder tree, Create/Load items, Export/Import items** |
| Specifications | Browse specification definitions |
| Application | View application paths and configuration info |
| **Profiles** | **Profile data copy between configurations with backup/restore** |

---

## Project Structure

```
FabricationSample/
├── Sample.cs                              # Entry point, FabAPI command
├── Commands/
│   ├── ExportCommands.cs                  # NETLOAD CSV export commands
│   └── ImportCommands.cs                  # NETLOAD CSV import commands
├── ContentTransfer/                       # ITM cross-config export/import
│   ├── Models/
│   │   └── ContentPackage.cs              # Manifest data models
│   ├── Services/
│   │   ├── ItemContentExportService.cs    # Export logic + manifest generation
│   │   └── ItemContentImportService.cs    # Import logic + reference re-resolution
│   └── Windows/
│       ├── ItemExportWindow.xaml(.cs)      # Export selection UI
│       └── ItemImportWindow.xaml(.cs)      # Import preview + override UI
├── ProfileCopy/                           # Profile data copy feature
│   ├── Models/
│   ├── Services/
│   └── Windows/
├── Services/
│   ├── Export/                            # CSV export service implementations
│   ├── Import/                            # CSV import service implementations
│   └── ItemSwap/                          # Item swap with undo
├── Models/                                # Shared data models
├── UserControls/
│   ├── DatabaseEditor/                    # Main tabbed UI (partial class)
│   │   ├── DatabaseEditor.xaml            # XAML layout for all tabs
│   │   ├── DatabaseEditor.xaml.cs         # Core code-behind
│   │   ├── DatabaseEditor-ContentTransfer.cs  # Export/Import button handlers
│   │   ├── DatabaseEditor-Import.cs       # CSV import handlers
│   │   └── ...                            # Other partial class files
│   ├── ServiceEditor/                     # Service editing views
│   └── ItemFolders/                       # Item folder tree view
├── Windows/                               # Shared dialog windows
└── Utilities/                             # Helper classes
```

---

## Building from Source

### Requirements

- Visual Studio 2022
- .NET Framework 4.8 targeting pack
- AutoCAD 2024 (for API DLLs: `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll`)
- Fabrication 2024 CADmep (for `FabricationAPI.dll`)

### Build Command

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" FabricationSample.sln /p:Configuration=Release /p:Platform=x64
```

Output: `bin\x64\Release\FabricationSample.dll`



---

## Known Bugs

### Item Swap: Replaced items may not return to original coordinates

**Affects:** Item Swap with Undo

When swapping an item and then undoing the swap, the restored item may not be placed back at its original coordinates. This occurs because:

1. **Designline-connected items** (nodes and fittings) are constrained by their connections to adjacent items on the designline. The Fabrication API does not provide a way to reinsert an item at a specific position along a designline, so the restored item may appear at a default location instead of its original position.

2. **Connector-based repositioning limitations.** The swap undo uses the primary connector endpoint (connector index 0) to calculate a move offset. If the replacement item has different connector geometry or a different connector count than the original, the offset calculation may not align correctly.

3. **AutoCAD MOVE command timing.** The repositioning uses `SendStringToExecute` to issue an AutoCAD `_.MOVE` command asynchronously. If AutoCAD is busy processing other commands or the document context changes between the add and move operations, the move may silently fail. The fallback method (direct `TransformBy` via transaction) can also fail for Fabrication items that have internal position constraints.

**Workaround:** After undoing a swap, manually move the restored item to the correct position. For items on designlines, you may need to delete and re-add the item at the correct location along the run.
