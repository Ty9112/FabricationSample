# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FabricationSample is a .NET Framework 4.8 plugin for Autodesk AutoCAD 2024 with Fabrication CADmep integration. It provides a WPF UI for database management and export/import commands for fabrication data.

**Owner**: Tyler (tphillips@harriscompany.com)

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
├── Services/
│   ├── Export/            # Export service implementations
│   │   ├── IExportService.cs
│   │   ├── CsvExportService.cs
│   │   ├── ProductInfoExportService.cs
│   │   ├── PriceTablesExportService.cs
│   │   ├── InstallationTimesExportService.cs
│   │   ├── ServiceTemplateDataExportService.cs
│   │   └── ...
│   ├── Import/            # Import service implementations
│   └── ItemSwap/          # Item swap with undo functionality
│       ├── ItemSwapService.cs
│       └── ItemSwapUndoManager.cs
├── Models/
│   ├── ItemPropertySnapshot.cs
│   └── ItemSwapUndoRecord.cs
├── UserControls/          # WPF user controls
│   ├── DatabaseEditor/    # Main database editing UI
│   ├── ServiceEditor/     # Service editing UI
│   └── ...
├── Utilities/
│   └── CsvHelpers.cs
└── Windows/               # Selection dialogs
```

## AutoCAD Commands

| Command | Description |
|---------|-------------|
| `FabAPI` | Opens main WPF UI window |
| `GetProductInfo` | Export products with prices and labor values |
| `ExportItemData` | Export service items with product list entries |
| `GetPriceTables` | Export price lists and breakpoint tables |
| `GetInstallationTimes` | Export installation times tables |
| `GetItemLabor` | Items with calculated labor from breakpoint tables |
| `GetItemInstallationTables` | Items with assigned installation tables |
| `GetServiceTemplateData` | Export service template data with selection dialog |
| `ImportProductList` | Import product list from CSV |
| `ImportPriceList` | Import price list from CSV |

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

## Related Projects

- `DiscordCADmep` - Simpler AutoCAD plugin with similar export commands (same repo parent)
- `fabrication-api-xmldocs` - API documentation extracted from FabricationAPI.chm
