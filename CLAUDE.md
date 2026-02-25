# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FabricationSample is a .NET Framework 4.8 plugin for Autodesk AutoCAD 2024 with Fabrication CADmep integration. It provides a WPF UI for database management, export/import commands for fabrication data, and an HTTP bridge service for external tool integration.

**Owner**: Tyler (tphillips@harriscompany.com)
**Current Version**: v1.2.0

## Build

```bash
msbuild FabricationSample.sln /p:Configuration=Release /p:Platform=x64
```

Output: `bin/x64/Release/FabricationSample.dll`

Debug (F5) launches AutoCAD 2024 with the plugin loaded.

## Dependencies

- **AutoCAD 2024 APIs**: `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` (from `C:\Program Files\Autodesk\AutoCAD 2024\`)
- **Fabrication API**: `FabricationAPI.dll` (from `C:\Program Files\Autodesk\Fabrication 2024\CADmep\`)
- **WPF**: PresentationFramework, PresentationCore, WindowsBase
- **.NET Framework 4.8**

## Architecture

```
FabricationSample/
├── Sample.cs              # Entry point: IExtensionApplication, FabAPI command
├── Commands/
│   ├── ExportCommands.cs  # NETLOAD commands for CSV export
│   └── ImportCommands.cs  # NETLOAD commands for data import
├── Data/
│   └── DataMapping.cs     # Data mapping utilities
├── Services/
│   ├── Bridge/
│   │   └── FabricationBridgeService.cs  # HTTP bridge at localhost:5050
│   ├── Export/
│   │   ├── IExportService.cs
│   │   ├── CsvExportService.cs
│   │   ├── ProductInfoExportService.cs
│   │   ├── PriceTablesExportService.cs
│   │   ├── InstallationTimesExportService.cs
│   │   ├── ServiceTemplateDataExportService.cs
│   │   ├── RevitBridgeExportService.cs  # Flat CSV for Dynamo/Power BI (v1.2.0)
│   │   └── ...
│   ├── Import/            # Import service implementations
│   └── ItemSwap/          # Item swap with undo functionality
├── Models/
│   ├── ItemPropertySnapshot.cs
│   └── ItemSwapUndoRecord.cs
├── ProfileCopy/
│   ├── Services/
│   │   └── ProfileCompareService.cs     # MAP file diff (v1.2.0)
│   └── Windows/
│       └── ProfileCompareWindow.xaml    # Profile comparison UI (v1.2.0)
├── UserControls/
│   ├── DatabaseEditor/    # Main database editing UI (partial classes)
│   │   ├── DatabaseEditor.xaml          # Tab container
│   │   ├── DatabaseEditor-Job.cs        # Job items tab
│   │   ├── DatabaseEditor-Services.cs   # Services tab
│   │   ├── DatabaseEditor-ServiceTemplates.cs
│   │   ├── DatabaseEditor-DataHealth.cs # Validation dashboard (v1.2.0)
│   │   ├── DatabaseEditor-ManageContent.cs  # Content management (v1.2.0)
│   │   ├── DatabaseEditor-Relationships.cs  # Relationship editor (v1.2.0)
│   │   ├── DatabaseEditor-Search.cs     # Database search (v1.2.0)
│   │   └── DatabaseEditor-Materials.cs
│   ├── ServiceEditor/     # Service editing UI
│   └── ItemFolders/       # Item folder browser
├── Utilities/
│   └── CsvHelpers.cs
└── Windows/
    ├── ConditionMappingWindow.xaml   # Condition mapping dialog (v1.2.0)
    └── TemplateComposerWindow.xaml   # Template composer (v1.2.0)
```

## AutoCAD Commands

| Command | Description |
|---------|-------------|
| `FabAPI` | Opens main WPF UI window |
| `GetProductInfo` | Export products with prices and labor values (see note below) |
| `ExportItemData` | Export service items with product list entries |
| `GetPriceTables` | Export price lists and breakpoint tables |
| `GetInstallationTimes` | Export installation times tables |
| `GetItemLabor` | Items with calculated labor from breakpoint tables |
| `GetItemInstallationTables` | Items with assigned installation tables |
| `GetServiceTemplateData` | Export service template data with selection dialog |
| `ImportProductList` | Import product list from CSV |
| `ImportPriceList` | Import price list from CSV |

### GetProductInfo — IsProductListed Column

The `GetProductInfo` CSV export produces ~236K rows, but **not all rows are real products**:

| `IsProductListed` Value | Meaning | Count | In MAP Product Database? |
|-------------------------|---------|-------|--------------------------|
| `"No"` | Item IS in the product information editor (MAP product database). The "No" refers to a "listed" flag, NOT to database presence. | Part of 164,850 | YES |
| `"Yes"` | Item IS in the product information editor and is listed. | Part of 164,850 | YES |
| `"N/A"` | Item is NOT in the MAP product database. These are ITM files sitting in item folders that have never been imported into the product editor. | 71,639 | NO |

**Real product count = "Yes" + "No" = 164,850.** The "N/A" rows should be excluded from product-level analysis (pricing, HPH mapping, labor lookups, etc.).

**Do NOT change the export command** to filter these out — too many downstream systems (fabrication-mcp, HPH mappers, Power BI) are connected to the current CSV format and handle the filtering themselves.

The CSV also has **3 duplicate "Id" columns** — use positional indexing (`csv.reader`), not `DictReader`.

### N/A Values — CRITICAL Rule

**`N/A` is the Fabrication database's default empty/null placeholder, NOT a valid value.**
Always treat `N/A` as empty/null/0 when counting, filtering, or aggregating ANY field
(Harrison codes, discount codes, Ferguson codes, sizes, specifications, status — everything).
Never count items with `N/A` as "having" a value assigned.

## FabricationBridgeService (localhost:5050)

The bridge exposes Fabrication database data over HTTP for external tools:

```
GET /api/products?search=...     # Search products
GET /api/products/{id}           # Product detail (by DatabaseId)
GET /api/products/{id}/image     # Product image
GET /api/services                # All services
GET /api/pricelists              # Price lists + breakpoint tables
GET /api/installtimes            # Installation times tables
GET /api/jobitems                # Items in current job
```

**Consumers**:
- XbimWebUI (Harris 3D Viewer) — product detail, images, pricing
- fabrication-mcp MCP server — wraps as 15 live MCP tools

**Key fix (v1.2.0)**: All 6 static ID-keyed dictionaries use `StringComparer.OrdinalIgnoreCase`
to handle mixed-case product IDs (e.g., `MDSK_NIB_000142-0001`) correctly with
`ToLowerInvariant()` URL routing.

**Database identity fields** (multi-model hub support):
`GET /api/status` and `GET /api/cache/status` include:
- `database_path` — full filesystem path to active Fabrication database
- `database_name` — folder name only (e.g., `Harris Wetside Database 2_0`)
- `profile_name` — AutoCAD profile name (from `Application.CurrentProfile`)

These are consumed by the fabrication-mcp hub system (`get_active_profile`, `get_database_summary`)
and the XbimWebUI hub landing page to identify which database the bridge is serving.

## Key Patterns

### Export Services
All export services implement `IExportService` and use `CsvExportService` for file writing. Pattern:
```csharp
var service = new ProductInfoExportService();
service.Export(outputPath);
```

### Fabrication API Access
```csharp
using FabDB = Autodesk.Fabrication.DB.Database;

var services = FabDB.Services;           // Get all services
var products = FabDB.ProductDatabase;    // Get product database
var items = FabDB.Items;                 // Get items in current job
```

### BreakPointTable Value Access
```csharp
// GetValue takes (columnIndex, rowIndex) - column first!
DBOperationResult result = table.GetValue(columnIndex, rowIndex);
if (result.Status == ResultStatus.Succeeded)
    value = (double)result.ReturnObject;
```

### DatabaseEditor Partial Classes
The DatabaseEditor uses partial classes to split tab functionality across files.
Each `DatabaseEditor-*.cs` file adds methods for one tab. The XAML container is
`DatabaseEditor.xaml`. Add new tabs by creating a new partial class file.

## Version Control & Release Policy

**IMPORTANT — read before committing anything binary:**

- `Compiled/FabricationSample.dll` is **gitignored** and must NEVER be committed or pushed to the public repo (`Ty9112/FabricationSample`)
- All builds — including internal builds and any autodesk-mcp generated builds — stay local only
- Source code changes commit normally to `master`; the compiled DLL does not

### Publishing a Release

Official DLL releases are distributed exclusively via **GitHub Releases**:

```bash
# 1. Tag the commit
git tag v1.0.0
git push origin v1.0.0

# 2. On GitHub: Releases → New Release → select tag → attach FabricationSample.dll
```

### Release Reminder (for agents)

After a significant batch of commits (roughly 10+ commits or major feature completion), prompt Tyler:
> "We've made X commits since the last release. Want to publish a new GitHub Release and attach the latest DLL?"

## Current Branch

`master` - main public branch

## TODOs

- `ImportCommands.cs:246` — Re-enable product list import when API type issues are resolved
- `ImportCommands.cs:381` — Implement price list selection dialog
- `DatabaseEditor-Materials.cs:110` — Consider material usage cloner

### Recently Resolved
- ~~`ItemFoldersView.xaml.cs:91` — Handle adding new folders~~ (Feb 2026 — folder creation with icon + lazy-load child)
- ~~`SupplierIdsConverter.cs:63` — ConvertBack threw NotImplementedException~~ (Feb 2026 — returns Binding.DoNothing)
- ~~Convert.ToDouble crash risk (8 locations)~~ (Feb 2026 — replaced with double.TryParse in 4 files)

## Related Projects

- `DiscordCADmep` - Simpler AutoCAD plugin with similar export commands (same repo parent)
- `fabrication-api-xmldocs` - API documentation extracted from FabricationAPI.chm
- `XbimWebUI` (Harris 3D Viewer) - Consumes bridge endpoints for product visualization
- `fabrication-mcp` - MCP server wrapping bridge + CSV exports as 33 tools (14 CSV + 15 live bridge + 4 estimate)
