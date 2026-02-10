# FabricationSample

Extended Autodesk Fabrication API Sample plugin for CADmep / ESTmep / CAMduct (Fabrication 2024).

> **This is a fork of [MartinMagallanes/FabricationSample](https://github.com/MartinMagallanes/FabricationSample)** with significant feature additions for production use. See [Summary of Extended Features](#summary-of-extended-features) below.

![Main Window - Job Items tab with left navigation showing all available tabs and Item Swap buttons](Examples/Screenshots/MainWindow.png)
*The FabAPI main window showing the Job Items tab with the full tab list on the left. The Swap Item and Undo Swap buttons (bottom-left) are extended features.*

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
  - [Commands Tab (Recommended Starting Point)](#commands-tab-recommended-starting-point)
  - [Content Transfer (Export/Import Items)](#content-transfer-exportimport-items)
  - [Profile Data Copy](#profile-data-copy)
    - [Selective Cleanup](#selective-cleanup)
    - [Push to Profiles (Global Only)](#push-to-profiles-global-only)
    - [OneDrive and Network Storage Considerations](#onedrive-and-network-storage-considerations)
  - [CSV Import (Product List, Price List)](#csv-import)
  - [Item Swap with Undo](#item-swap-with-undo)
- [Feature Risk Guide](#feature-risk-guide)
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

![Product Database tab with export and import buttons](Examples/Screenshots/ProductDatabase.png)
*The Product Database tab displays all product entries with their properties. Export and import buttons at the bottom allow CSV data transfer.*

![Services tab with service selection dialog for export](Examples/Screenshots/Services.png)
*The Services tab with the service selection dialog. Select which services to include when exporting service data or button reports.*

![Service Templates tab with template selection and button report export](Examples/Screenshots/ServiceTemplates.png)
*The Service Templates tab showing template conditions and button items. Export Button Report and Import Button Report are available for CSV-based template data management.*

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

### Commands Tab (Recommended Starting Point)

**Location:** FabAPI Window > **Commands** tab

![Commands tab with export preview window showing scrollable CSV data](Examples/Screenshots/Commands.png)
*The Commands tab with all exports and imports in one place. The Export Preview window (left) shows a scrollable DataGrid of the CSV data before you choose where to save it.*

If you're new to this plugin, the **Commands tab is the best place to start**. It provides a centralized hub for all export and import operations in a single, user-friendly interface — no need to navigate individual tabs or remember NETLOAD command names.

#### Why Start Here

The Commands tab consolidates every export and import operation into one place with:
- **Checkboxes** to select which commands to run
- **Quick-select buttons** (Select All, Select None, Exports Only, Imports Only)
- **Descriptions** for each command so you know what you're getting
- **Sequential execution** with a progress bar when running multiple commands
- **Scrollable CSV preview** before saving any export — see exactly what data you're getting before committing to a file

This is the safest way to explore the Fabrication database. Every export is **read-only** — it extracts data to CSV without modifying anything in the database. You can run all 7 exports to get a comprehensive snapshot of your configuration's data.

#### Available Export Commands

| Command | Description |
|---------|-------------|
| Get Product Info | Full product export with prices, labor, supplier IDs |
| Export Item Data | Service items with product list entries and conditions |
| Get Price Tables | Price lists and breakpoint tables (multi-file, prompts for table selection) |
| Get Installation Times | Installation times tables (multi-file, prompts for table selection) |
| Get Item Labor | Items with calculated labor from breakpoint tables |
| Get Item Installation Tables | Items with assigned installation table mappings |
| Get Service Template Data | Service template buttons, codes, and item paths (prompts for service selection) |

#### Available Import Commands

| Command | Description | Notes |
|---------|-------------|-------|
| Import Installation Times | Installation times from CSV | Active |
| Import Product Database | Product definitions and supplier IDs | Active |
| Import Supplier Discounts | Discount codes from CSV | Active |
| Import Button Report | Service template button codes | Active |
| Import Price List | Price data into a selected price list | Disabled — requires price list selection on Price Lists tab |

#### Export Preview

When you run an export command from the Commands tab, the data is first exported to a temporary file and displayed in a **scrollable preview window** with a DataGrid. You can:
- Scroll through all rows and columns to verify the data
- Resize the preview window by dragging edges or corners
- Click **Save As...** to choose where to save the file
- Click **Cancel** to discard without saving

For multi-file exports (Price Tables, Installation Times), the preview shows the first file from the set. After clicking Save As, all files are saved to the chosen folder.

#### How to Use

1. Open the FabAPI window and navigate to the **Commands** tab
2. Check the commands you want to run (use quick-select buttons for bulk selection)
3. Click **Run Selected Commands**
4. Confirm the list of commands to execute
5. Each command runs sequentially — for exports, you'll see a preview and choose where to save; for imports, you'll go through the standard file selection and column mapping workflow
6. A summary dialog shows results when all commands complete

---

### Content Transfer (Export/Import Items)

**Location:** FabAPI Window > **Manage Content** tab > **Export Items** / **Import Items** buttons

![Export Items — select items from the folder tree and choose an output folder](Examples/Screenshots/ExportItems.png)
*Export Items: Select .ITM files from the item folder tree (left), then choose an output folder (right). A manifest.json is generated with all database reference names.*

![Import Items — reference validation with green (ok) and yellow (not found) indicators](Examples/Screenshots/ImportItems.png)
*Import Items: Each item shows its database references with validation status. Green "(ok)" means the reference exists in the target configuration; yellow "(!) not found" means you need to assign a replacement from the dropdown.*

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

![Profiles tab with source profile selection, data type checkboxes, and Push to Profiles panel](Examples/Screenshots/Profiles.png)
*The Profiles tab showing copy from Global profile with selective data type checkboxes (Price & Labor, Primary, Secondary groups), quick-select buttons, and the Push to Profiles panel for pushing to multiple named profiles at once.*

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

#### OneDrive and Network Storage Considerations

Many organizations store their Fabrication database on OneDrive, SharePoint-synced folders, or network shares. Profile Data Copy writes `.MAP` files directly to these locations, which introduces several potential issues:

**OneDrive / SharePoint Sync:**
- **File locking:** OneDrive may hold a sync lock on `.MAP` files while uploading. If Profile Copy tries to overwrite a file that OneDrive is actively syncing, the copy can fail with an access denied error. The backup will have already been created, but the target file may be in an inconsistent state.
- **Sync delays:** After copying `.MAP` files, OneDrive may take seconds to minutes to sync the changes to other machines. If another user loads the profile on their machine before sync completes, they'll get the old data.
- **Conflict files:** If two users perform Profile Copy operations targeting the same profile simultaneously, OneDrive may create conflict copies (e.g., `Cost-DESKTOP-ABC123.MAP`) instead of overwriting. These conflict files are ignored by Fabrication and will cause the operations to appear to have no effect.
- **Cleanup file sync:** The `_pending_cleanup.json` file is written to the target profile's DATABASE folder. If OneDrive syncs this file to another machine before the original cleanup runs, the cleanup could run on the wrong machine or run twice.

**Network shares (UNC paths):**
- File locking on network shares can behave differently than local drives. If another user has the target profile loaded in ESTmep/CADmep (which holds `.MAP` files open), the copy will fail.
- Network latency can cause partial writes if the connection drops during a large `.MAP` file copy.

**Recommendations:**
1. **Coordinate with your team** — only one person should perform Profile Copy operations at a time, and other users should close ESTmep/CADmep before the copy begins
2. **Pause OneDrive sync** before performing Profile Copy if your database is on a OneDrive-synced path (right-click the OneDrive tray icon > Pause syncing)
3. **Wait for sync to complete** before having other users load the updated profiles
4. **Always keep backups enabled** (the checkbox is on by default) — this creates a timestamped backup before any changes, which can be restored from the Profiles tab if something goes wrong
5. **Check for conflict files** after a copy — look in the target profile's DATABASE folder for files with machine names appended. Delete these if found and re-run the copy.

---

### CSV Import

![Price Lists tab with column mapping window for CSV import](Examples/Screenshots/PriceLists.png)
*The Price Lists tab with the Column Mapping window open. Map your CSV columns to the expected import fields, preview the data, then import. Export and import buttons are highlighted at bottom-right.*

![Installation Tables tab with breakpoint data and column mapping for import](Examples/Screenshots/InstallationTables.png)
*The Installation Tables tab showing breakpoint table values with the Column Mapping window for importing installation times from CSV. Update, Import, and Export buttons are at the bottom-right.*

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

## Feature Risk Guide

Not all features carry the same risk. Here's a quick reference to help you decide what's safe to try immediately and what deserves more caution.

### Safe — Read-Only Operations (Start Here)

These features **do not modify the Fabrication database**. They only read data and export it to files. You can run these freely without any risk to your configuration.

| Feature | What It Does |
|---------|-------------|
| **Commands Tab > All Exports** | Exports data to CSV files. Preview before saving. No database changes. |
| **NETLOAD Export Commands** | Same exports via command line. Read-only. |
| **Application Tab** | Displays configuration info. No changes. |

**Recommendation:** Start with the Commands tab. Run all 7 exports to get a full snapshot of your database. Review the CSV previews to understand your data before attempting any imports or copies.

### Moderate Risk — Data Imports

These features **modify records in the Fabrication database** by updating or adding data. Changes are applied directly — there is no built-in undo for imports (unlike Item Swap).

| Feature | Risk | Mitigation |
|---------|------|-----------|
| **Import Installation Times** | Updates existing installation rate records | Validate column mapping carefully; preview shows update vs. new counts before confirming |
| **Import Product Database** | Updates product definitions and supplier IDs | Only modifies products matching by ID; preview shows exactly what changes |
| **Import Supplier Discounts** | Updates discount codes | Limited scope — only affects discount values |
| **Import Button Report** | Updates service template button codes | Only modifies matching buttons by service/tab/name |
| **Import Price List** | Updates price list entries | Requires selecting the target price list first on the Price Lists tab |

**Recommendations:**
1. **Export first, import second.** Always run the corresponding export (e.g., Get Product Info) before importing so you have a baseline to compare against
2. **Review the preview carefully.** The column mapping window and import preview show you exactly what will change. If the counts look wrong, cancel and check your CSV
3. **Test on a non-production profile first.** Copy your profile, import into the copy, verify the results, then repeat on the real profile
4. **Back up your .MAP files** before importing. Profile Data Copy's backup feature can help, or just manually copy the DATABASE folder

### Higher Risk — File-Level Operations

These features **copy or overwrite database files (.MAP files) and item files (.ITM files)**. They operate at the file level, which means mistakes can affect the entire profile or configuration.

| Feature | Risk | Mitigation |
|---------|------|-----------|
| **Profile Data Copy** | Overwrites target profile's .MAP files | Automatic backup created before copy; can restore from Profiles tab |
| **Push to Profiles** | Overwrites .MAP files across multiple profiles at once | Same backup protection, but applied to many profiles simultaneously — a mistake affects all targets |
| **Content Transfer Export** | Copies .ITM files out — **read-only**, safe | No risk to source configuration |
| **Content Transfer Import** | Copies .ITM files in and re-resolves database references | Can create items with unresolved references if names don't match; duplicate DB ID warning exists but proceeding overwrites existing items |
| **Item Swap** | Replaces items in a drawing | Has undo, but restored items may not return to original coordinates (see Known Bugs) |

**Recommendations:**
1. **Always leave "Create backup" checked** when using Profile Data Copy. This is on by default — don't turn it off
2. **Coordinate with your team.** Other users should close ESTmep/CADmep before you push to their profiles. See the [OneDrive section](#onedrive-and-network-storage-considerations) for additional concerns with shared storage
3. **Start with a single profile** before using Push to Profiles. Copy to one named profile, load it, verify everything looks right, then push to the rest
4. **For Content Transfer imports,** carefully review the reference validation in the import preview. Green "(ok)" means the reference exists in the target; yellow warnings mean you need to manually assign a replacement or the reference will be left unresolved
5. **Restart AutoCAD/ESTmep after Profile Data Copy.** The Fabrication API loads .MAP files at startup and caches them in memory. Copied files won't take effect until the next session

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
| **Commands** | **Centralized hub for all export/import operations with preview and progress** |
| Application | View application paths, configuration info, What's New, and Resources |
| **Profiles** | **Profile data copy between configurations with backup/restore** |

![Application tab showing configuration info, What's New, and Resources](Examples/Screenshots/Application.png)
*The Application tab displays the current Fabrication configuration details, a "Features Added in This Fork" summary, and a Resources section with a link to the GitHub README.*

![Service Properties view with export and import service entries](Examples/Screenshots/ServiceProperties.png)
*The Service Properties view showing detailed service entry data with export and import capabilities for service-level data.*

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
│   │   ├── DatabaseEditor-Commands.cs     # Commands tab logic and command registry
│   │   ├── DatabaseEditor-ContentTransfer.cs  # Export/Import button handlers
│   │   ├── DatabaseEditor-Import.cs       # CSV import handlers
│   │   └── ...                            # Other partial class files
│   ├── ServiceEditor/                     # Service editing views
│   └── ItemFolders/                       # Item folder tree view
├── Windows/                               # Shared dialog windows
│   ├── ExportPreviewWindow.xaml(.cs)      # Scrollable CSV preview before save
│   └── ...                                # Selection and editing dialogs
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
