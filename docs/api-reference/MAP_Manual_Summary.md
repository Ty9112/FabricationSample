# MAP Manual Summary for FabricationSample Integration

> **Source**: MAP Software Manual (1,297 pages)
> **Legacy Software**: EST-Mech, EST-Duct, CAD-Mech, CAD-Duct, CAM-Duct
> **Modern Equivalent**: Autodesk Fabrication (ESTmep, CADmep, CAMduct)

---

## Table of Contents

1. [Database Architecture Overview](#database-architecture-overview)
2. [MAPProd (Product Information Database)](#mapprod-product-information-database)
3. [Price Lists](#price-lists)
4. [Labor Tables (Fabrication & Installation Times)](#labor-tables)
5. [Services Database](#services-database)
6. [Product Lists (.ITM Files)](#product-lists)
7. [Mapping to FabricationSample Project](#mapping-to-fabricationsample-project)
8. [Enhancement Opportunities](#enhancement-opportunities)

---

## Database Architecture Overview

MAP Software uses a **shared database architecture** across all applications:

```
PM Shared/
├── DATABASE/
│   ├── prodinfo.map          # MAPProd - Product Information Database
│   ├── [Price Lists]         # Material pricing data
│   ├── [Labor Tables]        # Fabrication & Installation times
│   └── [Services]            # Report categories, CAD layering
├── LIBRARIES/                # Item folders, patterns
├── REPORTS/                  # Report templates
└── PROJECTS/                 # Job files (.MAJ)
```

### Five Core Databases

| Database | Purpose | FabricationSample Equivalent |
|----------|---------|------------------------------|
| **Main Database** | Materials, company settings, custom data fields | `Database.Materials`, `Database.CustomData` |
| **Pattern Database** | Construction standards, pressure classes, pipe ODs, hangers | `Database.Sections`, `Database.Specifications` |
| **Estimating Database** | Price lists, labor tables, overhead, rates | `Database.PriceLists`, `Database.FabricationTimesTable` |
| **Services Database** | Report categories, CAD layering, system/service setup | `Database.Services`, `Database.ServiceTypes` |
| **Profile Database** | CAM cutting profiles (ductwork only) | N/A for estimating |

---

## MAPProd (Product Information Database)

### What It Is
MAPProd.exe is the **Product Information Editor** that manages metadata for all product IDs. This data enriches reports and makes price lists easier to navigate.

### MAPProd Fields

| Field | Description | Fabrication API Property |
|-------|-------------|-------------------------|
| **ID** | Unique product code (Harrison code) | `ProductDefinition.Id` |
| **Group** | Pricing group (usually "Harrison") | `ProductDefinition.Group` |
| **Supplier** | Manufacturer name | `ProductDefinition.Supplier` |
| **Material** | Material type (Carbon Steel, Copper, etc.) | `ProductDefinition.Material` |
| **Description** | Long-form product name | `ProductDefinition.Description` |
| **Size** | Product size designation | `ProductDefinition.Size` |
| **Pressure Class** | Pressure rating (150#, 3000#, Standard) | `ProductDefinition.PressureClass` |
| **Install Type** | Connection method (Welded, Flanged, Grooved) | `ProductDefinition.InstallType` |
| **Product** | Generic item type (Elbow, Valve, Tee) | `ProductDefinition.Product` |

### Data Flow
```
Product List (.ITM) ──[ID Code]──> MAPProd ──[enriches]──> Price Lists & Reports
```

### Current FabricationSample Implementation
Your `DatabaseEditor-MapProd.cs` already implements:
- View/filter `ProductDatabase.ProductDefinitions`
- Create `ProductDefinition`, `ProductGroup`, `ProductSupplier`
- Edit via `AddEditProductDBWindow`
- Save with `ProductDatabase.Save()`

---

## Price Lists

### Two Types of Price Lists

#### 1. Product List Type (Code-Based)
Used for **bought-out items** (valves, fittings, equipment) with unique product codes.

| Column | Description |
|--------|-------------|
| **ID** | Harrison/product code (matches Item Product List) |
| **Cost** | List price |
| **Discount** | Percentage, multiplier, or discount code |
| **Units** | Per Each or Per Foot |
| **Date** | Last update date |
| **Status** | A=Active, P=Price on Application, D=Discontinued |

#### 2. Breakpoint Type (Size-Based)
Used for **manufactured items** (ductwork, fabricated fittings) with size-dependent pricing.

```
         │ 26 Gauge │ 24 Gauge │ 22 Gauge │
─────────┼──────────┼──────────┼──────────┤
4"       │   $4.94  │   $5.60  │   $6.25  │
6"       │   $6.50  │   $7.30  │   $8.10  │
8"       │   $8.25  │   $9.20  │  $10.15  │
```

**Row Variable**: Dimension (Width, Diameter, Area)
**Column Variable**: Gauge, Material thickness, or other property

### Fabrication API Mapping

```csharp
// Product List Type
PriceList priceList = Database.PriceLists["Harrison - Nibco"];
ProductEntry entry = priceList.Entries.FirstOrDefault(e => e.Id == "004NI1234");
double cost = entry.Cost;
double discount = entry.Discount;

// Breakpoint Type
PriceList breakpointList = Database.PriceLists["Ductwork - Rectangular"];
// GetValue(columnIndex, rowIndex) - column first!
DBOperationResult result = breakpointList.GetValue(gaugeCol, sizeRow);
```

---

## Labor Tables

### Two Types of Labor Tables

#### 1. Product List Type
Used for items with unique product codes - maps ID to labor hours.

| Column | Description |
|--------|-------------|
| **ID** | Product code |
| **Time** | Labor hours |
| **Units** | Per Each or Per Foot |

#### 2. Breakpoint Type
Size-dependent labor tables with row/column breakpoints.

### Labor Table Components

| Component | Description |
|-----------|-------------|
| **Table Name** | Identifier for the labor table |
| **Table Group** | Organization category (Dampers, Fittings, etc.) |
| **Value Sets** | Multiple sheets for different conditions |
| **Labor Rate** | $/hour category (Journeyman, Helper, etc.) |
| **Adjust Code** | Multiplier applied at bid time |
| **Include If** | Conditions (Single Wall, Double Wall, Custom Query) |

### Fabrication API Mapping

```csharp
// Fabrication Times (F-Rate)
FabricationTimesTable fabTable = Database.FabricationTimesTables["Shop Labor"];
ProductEntry entry = fabTable.Entries.FirstOrDefault(e => e.Id == productId);

// Installation Times (E-Rate)
InstallationTimesTable installTable = Database.InstallationTimesTables["Field Labor"];

// Breakpoint table access
DBOperationResult result = table.GetValue(columnIndex, rowIndex);
if (result.Status == ResultStatus.Succeeded)
    laborHours = (double)result.ReturnObject;
```

### Labor Rate Categories (Sections + Rates)

| Rate Category | Typical Use |
|---------------|-------------|
| Journeyman Shop | Skilled fabrication labor |
| Journeyman Field | Skilled installation labor |
| Helper | Apprentice/assistant labor |
| Delivery | Material handling |
| Foreman | Supervision overhead |

---

## Services Database

### Purpose
Services define **report categories**, **CAD layering**, and **takeoff system setup**.

### Service Components

| Component | Description |
|-----------|-------------|
| **Service Name** | System identifier (Cold Water, Hot Water, Sanitary) |
| **Service Type** | Sub-category for layering (Supply, Return, Waste) |
| **Status** | Drawing status codes |
| **CAD Layer Settings** | Layer naming, colors, linetypes |

### Fabrication API

```csharp
var services = Database.Services;
foreach (Service service in services)
{
    string name = service.Name;
    var types = service.Types;  // ServiceType collection
}
```

---

## Product Lists

### What They Are
Product Lists are **pre-configured size ranges** for parametric items. Instead of one item per size, a single Product List contains all available sizes.

### Product List Structure

| Field | Purpose |
|-------|---------|
| **Name** | Size designation (populates selection dropdown) |
| **Dimensions** | Locked/unlocked dimension values |
| **Options** | Configuration options (segments, angles) |
| **ID (Database ID)** | Links to MAPProd, Price Lists, Labor Tables |
| **Order Code** | Manufacturer order number |
| **Revision** | Version tracking |

### Fabrication API

```csharp
// Create Product List
ItemProductList prodList = new ItemProductList();
ItemProductListDataTemplate template = new ItemProductListDataTemplate();

// Add dimension definitions
template.AddDimensionDefinition(
    new ItemProductListDimensionDefinition(dimDef, locked: true),
    defaultValue);

// Add rows
prodList.AddRow(
    name: "4\"",
    alias: "4-inch",
    databaseId: "004NI1234",
    orderCode: "VENDOR-123",
    ...
);

// Apply to item
ContentManager.CreateProductItem(item, prodList);
```

---

## Mapping to FabricationSample Project

### Currently Implemented

| MAP Feature | FabricationSample File | Status |
|-------------|------------------------|--------|
| MAPProd viewing/editing | `DatabaseEditor-MapProd.cs` | Complete |
| Product Groups | `DatabaseEditor-MapProd.cs` | Complete |
| Product Suppliers | `DatabaseEditor-MapProd.cs` | Complete |
| Services | `DatabaseEditor-Services.cs` | Complete |
| Materials | `DatabaseEditor-Materials.cs` | Complete |
| Fabrication Times | `DatabaseEditor-FabTimes.cs` | Complete |
| Price List Export | `PriceTablesExportService.cs` | Complete |
| Installation Times Export | `ItemInstallationTablesExportService.cs` | Complete |

### API Classes Used

```csharp
// Database Access
using FabDB = Autodesk.Fabrication.DB.Database;

// MAPProd equivalent
ProductDatabase         // Collection of ProductDefinitions
ProductDefinition       // Single product entry
ProductGroup           // Grouping category
ProductSupplier        // Manufacturer info

// Price Lists
PriceList              // Price table
ProductEntry           // Entry in price list

// Labor Tables
FabricationTimesTable   // Shop labor (F-Rate)
InstallationTimesTable  // Field labor (E-Rate)
BreakPointTable        // Size-based lookup table

// Services
Service                // Service definition
ServiceType            // Sub-category
```

---

## Enhancement Opportunities

Based on MAP Manual features not yet in FabricationSample:

### 1. Harrison Price Import
**MAP Feature**: Import pricing from Harrison e-Office CSV exports
**Implementation**: Add `HarrisonPriceImportService.cs`

```csharp
public class HarrisonPriceImportService : IImportService
{
    // Import columns: ID, Cost, Discount, Date, Status
    public void ImportFromCsv(string filePath, PriceList targetList) { }
}
```

### 2. Adjust Codes for Labor Tables
**MAP Feature**: Apply multipliers to labor tables at bid time
**Implementation**: Expose `AdjustCode` property on labor tables

### 3. Value Sets / Conditions
**MAP Feature**: Multiple labor value sets based on conditions (Single Wall vs Double Wall)
**Implementation**: UI for managing `ValueSet` conditions

### 4. MAPProd Excel Export/Import
**MAP Feature**: Bulk edit product info in Excel
**Implementation**: Add to existing export services

```csharp
public class ProductInfoExportService
{
    public void ExportToExcel(string filePath)
    {
        // Export: ID, Group, Supplier, Material, Description,
        //         Size, PressureClass, InstallType, Product
    }
}
```

### 5. Generic Names for Price List Comparison
**MAP Feature**: Map different supplier tables for quick cost comparison
**Implementation**: Add `GenericName` mapping UI

### 6. Breakpoint Table Editor
**MAP Feature**: Visual grid editor for breakpoint tables
**Implementation**: WPF DataGrid with row/column variable configuration

---

## Quick Reference: MAP to Fabrication API

| MAP Term | Fabrication API |
|----------|-----------------|
| MAPProd | `ProductDatabase`, `ProductDefinition` |
| Price List (Product) | `PriceList` with `ProductEntry` |
| Price List (Breakpoint) | `PriceList` with `BreakPointTable` |
| Fabrication Times | `FabricationTimesTable` (F-Rate) |
| Installation Times | `InstallationTimesTable` (E-Rate) |
| Services | `Database.Services` |
| Service Type | `ServiceType` |
| Item Folders | `Database.ItemFolders` |
| Product List | `ItemProductList` |
| Harrison Code | `ProductEntry.Id`, `ProductDefinition.Id` |

---

## File Locations Reference

| MAP Location | Fabrication 2024 Location |
|--------------|---------------------------|
| `PM Shared/Database/prodinfo.map` | `%APPDATA%\Autodesk\Fabrication 2024\` |
| `PM Shared/Libraries/` | Database Items folder |
| `PM Shared/Projects/` | Job files |

---

*Generated from MAP Manual (1,297 pages) - February 2026*
