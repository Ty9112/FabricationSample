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

## Current Branch

`feature/item-swap-with-undo` - Adding item swap functionality with undo support

## Related Projects

- `DiscordCADmep` - Simpler AutoCAD plugin with similar export commands (same repo parent)
- `fabrication-api-xmldocs` - API documentation extracted from FabricationAPI.chm
