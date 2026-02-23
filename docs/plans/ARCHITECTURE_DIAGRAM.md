# Enhanced FabricationSample - Architecture Diagram

## System Architecture Overview

```
┌────────────────────────────────────────────────────────────────────────────┐
│                            USER INTERFACE LAYER                             │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────┐    ┌──────────────────────────────────────────┐ │
│  │  NETLOAD Commands   │    │        WPF UI Application                 │ │
│  ├─────────────────────┤    ├──────────────────────────────────────────┤ │
│  │                     │    │                                           │ │
│  │  ExportItemData     │    │  ┌─────────────────────────────────────┐ │ │
│  │  ExportPriceTables  │    │  │   FabricationWindow (Main UI)       │ │ │
│  │  ExportProductInfo  │    │  └─────────────────────────────────────┘ │ │
│  │  ExportInstallTimes │    │                                           │ │
│  │  ExportItemLabor    │    │  ┌─────────────────┬──────────────────┐ │ │
│  │                     │    │  │ DatabaseEditor  │  ItemEditor      │ │ │
│  │  ImportProductList  │    │  ├─────────────────┼──────────────────┤ │ │
│  │  ImportPriceList    │    │  │ • Export Tab    │ • Product List   │ │ │
│  │                     │    │  │ • Price Lists   │   Import/Export  │ │ │
│  │  FabAPI (UI Launch) │    │  │ • Install Times │ • Item Editing   │ │ │
│  │  FabExport          │    │  └─────────────────┴──────────────────┘ │ │
│  │  FabImport          │    │                                           │ │
│  │                     │    │  ┌─────────────────┬──────────────────┐ │ │
│  └─────────────────────┘    │  │ ServiceEditor   │  New Dialogs     │ │ │
│           │                 │  ├─────────────────┼──────────────────┤ │ │
│           │                 │  │ • Service Mgmt  │ • Export Config  │ │ │
│           │                 │  │ • Templates     │ • Import Preview │ │ │
│           │                 │  └─────────────────┴──────────────────┘ │ │
│           │                 │                                           │ │
│           └─────────────────┼───────────────────────────────────────────┘ │
│                             │                                             │
└─────────────────────────────┼─────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                           BUSINESS LOGIC LAYER                              │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                      Export Services                                  │ │
│  ├──────────────────────────────────────────────────────────────────────┤ │
│  │                                                                       │ │
│  │  ┌──────────────────┐  ┌──────────────────┐  ┌───────────────────┐ │ │
│  │  │ ItemExportService│  │PriceExportService│  │InstallExportService│ │ │
│  │  ├──────────────────┤  ├──────────────────┤  ├───────────────────┤ │ │
│  │  │ • Item data      │  │ • Price lists    │  │ • Install tables  │ │ │
│  │  │ • Item labor     │  │ • Breakpoint BP  │  │ • Breakpoint BP   │ │ │
│  │  │ • Install tables │  │ • Product prices │  │ • Labor values    │ │ │
│  │  └──────────────────┘  └──────────────────┘  └───────────────────┘ │ │
│  │                                                                       │ │
│  │  ┌──────────────────────────────────────────────────────────────┐   │ │
│  │  │         CsvExportService (Base Class)                        │   │ │
│  │  ├──────────────────────────────────────────────────────────────┤   │ │
│  │  │ • GenerateCsvData() - Abstract method                        │   │ │
│  │  │ • Export() - Common export logic                             │   │ │
│  │  │ • ReportProgress() - Progress tracking                       │   │ │
│  │  │ • Cancel() - Cancellation support                            │   │ │
│  │  └──────────────────────────────────────────────────────────────┘   │ │
│  │                                                                       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                      Import Services                                  │ │
│  ├──────────────────────────────────────────────────────────────────────┤ │
│  │                                                                       │ │
│  │  ┌────────────────────┐  ┌────────────────────┐  ┌───────────────┐ │ │
│  │  │ CsvImportService   │  │ ValidationService  │  │ Import Models │ │ │
│  │  ├────────────────────┤  ├────────────────────┤  ├───────────────┤ │ │
│  │  │ • Parse CSV        │  │ • Header validation│  │ • ImportResult│ │ │
│  │  │ • Map columns      │  │ • Data validation  │  │ • ImportRow   │ │ │
│  │  │ • Apply data       │  │ • Error reporting  │  │ • ImportError │ │ │
│  │  └────────────────────┘  └────────────────────┘  └───────────────┘ │ │
│  │                                                                       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │                   Shared Utilities                                    │ │
│  ├──────────────────────────────────────────────────────────────────────┤ │
│  │                                                                       │ │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │ │
│  │  │ CsvHelpers   │  │ FileHelpers  │  │ ProgressReporter         │  │ │
│  │  ├──────────────┤  ├──────────────┤  ├──────────────────────────┤  │ │
│  │  │• WrapForCsv()│  │• GetFolder() │  │• ReportProgress()        │  │ │
│  │  │• ParseCsv()  │  │• SaveFile()  │  │• UpdateUI()              │  │ │
│  │  │• Validate()  │  │• OpenFile()  │  │• HandleCancellation()    │  │ │
│  │  └──────────────┘  └──────────────┘  └──────────────────────────┘  │ │
│  │                                                                       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                                  ▼
┌────────────────────────────────────────────────────────────────────────────┐
│                            DATA ACCESS LAYER                                │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐ │
│  │              Autodesk Fabrication API (FabricationAPI.dll)           │ │
│  ├──────────────────────────────────────────────────────────────────────┤ │
│  │                                                                       │ │
│  │  Autodesk.Fabrication.DB                                             │ │
│  │  ├─ Database                                                          │ │
│  │  │  ├─ Services                   (Service hierarchy)                │ │
│  │  │  ├─ SupplierGroups             (Price lists)                      │ │
│  │  │  ├─ InstallationTimesTable     (Labor tables)                     │ │
│  │  │  ├─ Materials, Gauges, etc.    (Database entities)                │ │
│  │  │                                                                    │ │
│  │  Autodesk.Fabrication.Content                                        │ │
│  │  ├─ ContentManager                                                    │ │
│  │  │  ├─ LoadItem()                 (Load ITM files)                   │ │
│  │  │  ├─ CreateProductItem()        (Create product list items)        │ │
│  │  │                                                                    │ │
│  │  Autodesk.Fabrication (Core)                                         │ │
│  │  ├─ Item                          (Fabrication items)                │ │
│  │  │  ├─ ProductList                (Product list data)                │ │
│  │  │  ├─ Dimensions, Options        (Item properties)                  │ │
│  │  │  ├─ InstallationTimesTable     (Assigned labor table)             │ │
│  │  │                                                                    │ │
│  │  ProductDatabase                                                      │ │
│  │  ├─ ProductDefinitions            (Product catalog)                  │ │
│  │  ├─ Suppliers                     (Supplier information)             │ │
│  │  ├─ LookUpSupplierId()            (Product ID mapping)               │ │
│  │                                                                       │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
└────────────────────────────────────┬────────────────────────────────────────┘
                                     │
                                     ▼
                          ┌────────────────────┐
                          │  Fabrication       │
                          │  Database Files    │
                          │  (.mdb, .itm, etc) │
                          └────────────────────┘
```

## Data Flow Diagrams

### Export Data Flow

```
┌──────────────┐
│     USER     │
└──────┬───────┘
       │ 1. Initiates Export
       │    (Command or UI Button)
       ▼
┌──────────────────────┐
│  Command/Handler     │
│  ──────────────────  │
│  • Validate env      │
│  • Get export path   │
│  • Call service      │
└──────┬───────────────┘
       │ 2. Call Export Service
       ▼
┌──────────────────────┐
│  Export Service      │
│  ──────────────────  │
│  • Query API         │
│  • Transform data    │◄──────┐
│  • Report progress   │       │ 3. Read Data
│  • Generate CSV      │       │
└──────┬───────────────┘       │
       │ 4. Write CSV          │
       ▼                       │
┌──────────────────────┐       │
│  CsvHelpers          │       │
│  ──────────────────  │       │
│  • Format values     │       │
│  • Escape special    │       │
│  • Build rows        │       │
└──────┬───────────────┘       │
       │ 5. Save to File       │
       ▼                       │
┌──────────────────────┐   ┌───┴──────────────┐
│  File System         │   │ Fabrication API  │
│  ──────────────────  │   │ ────────────────  │
│  • ItemReport.csv    │   │ • Database       │
│  • PriceTables/      │   │ • ProductDatabase│
│  • ProductInfo.csv   │   │ • ContentManager │
└──────┬───────────────┘   └──────────────────┘
       │ 6. Open file
       ▼
┌──────────────────────┐
│  Excel/CSV Viewer    │
└──────────────────────┘
```

### Import Data Flow

```
┌──────────────┐
│     USER     │
└──────┬───────┘
       │ 1. Select CSV File
       ▼
┌──────────────────────┐
│  Import Handler      │
│  ──────────────────  │
│  • Open file dialog  │
│  • Read CSV          │
└──────┬───────────────┘
       │ 2. Parse CSV
       ▼
┌──────────────────────┐
│  CsvHelpers          │
│  ──────────────────  │
│  • Parse header      │
│  • Parse rows        │
│  • Handle escapes    │
└──────┬───────────────┘
       │ 3. Validate Data
       ▼
┌──────────────────────┐
│  ValidationService   │
│  ──────────────────  │
│  • Check headers     │
│  • Validate values   │
│  • Check duplicates  │
│  • Report errors     │
└──────┬───────────────┘
       │ 4. Preview
       ▼
┌──────────────────────┐
│  Import Preview UI   │
│  ──────────────────  │
│  • Show first 10 rows│
│  • Display errors    │
│  • Confirm import    │
└──────┬───────────────┘
       │ 5. User confirms
       ▼
┌──────────────────────┐
│  Import Service      │
│  ──────────────────  │
│  • Map columns       │
│  • Transform data    │─────────┐
│  • Apply to API      │         │ 6. Write Data
└──────┬───────────────┘         │
       │ 7. Success              │
       ▼                         ▼
┌──────────────────────┐   ┌─────────────────┐
│  Success Message     │   │ Fabrication API │
│  ──────────────────  │   │ ─────────────── │
│  • X rows imported   │   │ • Item.Product  │
│  • Refresh UI        │   │   List.AddRow() │
└──────────────────────┘   └─────────────────┘
```

## Module Dependencies

```
┌─────────────────────────────────────────────────────────────────┐
│                           Commands                              │
│  (NETLOAD entry points - minimal logic, delegates to services)  │
└──────────────────┬──────────────────────────────────────────────┘
                   │ depends on
                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                          Services                               │
│  (Business logic - no UI, no direct data access)                │
└──────────────────┬──────────────────────────────────────────────┘
                   │ depends on
                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                         Utilities                               │
│  (Pure functions - CSV, file I/O, validation)                   │
└─────────────────────────────────────────────────────────────────┘

                   ┌─ Both depend on ─┐
                   ▼                  ▼
┌──────────────────────────┐   ┌──────────────────────┐
│   Fabrication API        │   │   .NET Framework     │
│   (Autodesk provided)    │   │   (System.IO, etc)   │
└──────────────────────────┘   └──────────────────────┘
```

**Key Principles**:
1. **Commands** never access data directly - always call Services
2. **Services** contain all business logic - no UI dependencies
3. **Utilities** are pure functions - no state, no side effects
4. **UI** calls Services - never accesses API directly

## CSV File Structure Example

### Product Info Export (Multi-row per product)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Id  │IsListed│Group│...│Supplier1│Supplier2│(skip)│PriceList│Cost│(skip)│Labor│
├─────────────────────────────────────────────────────────────────────────────────┤
│ ABC │ Yes    │HVAC │...│ ABC-001 │ XYZ-123 │ N/A  │ List1   │ 5.2│ N/A  │ 0.5 │ ← Row 1: Product def + Price 1 + Labor 1
│ N/A │ N/A    │N/A  │...│ N/A     │ N/A     │ N/A  │ List2   │ 5.5│ N/A  │ N/A │ ← Row 2: Price 2 (same product)
│ N/A │ N/A    │N/A  │...│ N/A     │ N/A     │ N/A  │ N/A     │N/A │ N/A  │ 0.6 │ ← Row 3: Labor 2 (same product)
├─────────────────────────────────────────────────────────────────────────────────┤
│ DEF │ No     │Plumb│...│ DEF-002 │ (none)  │ N/A  │ List1   │ 3.1│ N/A  │ 0.3 │ ← Next product starts
└─────────────────────────────────────────────────────────────────────────────────┘
```

**Reading Logic**:
- Product definition only in first row where Id != "N/A"
- Subsequent rows contain additional prices or labor for same product
- "(skip)" columns help Excel/import tools identify section boundaries

## Component Interaction Sequence

### Export Command Execution Sequence

```
User types "ExportProductInfo" in AutoCAD
    │
    ├─► ExportCommands.ExportProductInfo() [CommandMethod]
    │       │
    │       ├─► CommandHelpers.ValidateFabricationLoaded()
    │       │       └─► Returns: true/false
    │       │
    │       ├─► CommandHelpers.PromptForExportLocation("Product Info")
    │       │       └─► Returns: "C:\Exports" or null
    │       │
    │       ├─► new ProductExportService()
    │       │       │
    │       │       ├─► service.ProgressChanged += (s, e) => Update AutoCAD command line
    │       │       │
    │       │       └─► service.ExportProductInfo("C:\Exports\ProductInfo.csv")
    │       │               │
    │       │               ├─► GenerateCsvData() [Abstract method implementation]
    │       │               │       │
    │       │               │       ├─► Loop through ProductDatabase.ProductDefinitions
    │       │               │       │       └─► CsvHelpers.WrapForCsv() for each field
    │       │               │       │
    │       │               │       ├─► Loop through Database.SupplierGroups.PriceLists
    │       │               │       │       └─► Add price rows for matching products
    │       │               │       │
    │       │               │       └─► Loop through Database.InstallationTimesTable
    │       │               │               └─► Add labor rows for matching products
    │       │               │
    │       │               ├─► File.WriteAllLines(path, csvData)
    │       │               │
    │       │               └─► Return: ExportResult { Success=true, FilePath="...", RowCount=5000 }
    │       │
    │       └─► CommandHelpers.ShowSuccess(filePath, rowCount)
    │               │
    │               └─► MessageBox: "Export complete. Open file?" → Process.Start(explorer.exe)
    │
    └─► Command complete
```

---

**Document Version**: 1.0
**Date**: 2026-01-09
