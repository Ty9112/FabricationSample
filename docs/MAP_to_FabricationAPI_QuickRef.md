# MAP to Fabrication API Quick Reference

## Database Access Pattern

```csharp
using FabDB = Autodesk.Fabrication.DB.Database;

// Access databases
var products = FabDB.ProductDatabase;      // MAPProd
var priceLists = FabDB.PriceLists;         // Price Lists
var fabTimes = FabDB.FabricationTimesTables;   // F-Rate
var installTimes = FabDB.InstallationTimesTables; // E-Rate
var services = FabDB.Services;             // Services Database
```

## MAPProd Operations

```csharp
// List all products
foreach (ProductDefinition def in ProductDatabase.ProductDefinitions)
{
    string id = def.Id;
    string supplier = def.Supplier?.Name;
    string material = def.Material?.Name;
    string description = def.Description;
}

// Filter by group
var group = ProductDatabase.ProductGroups.FirstOrDefault(g => g.Name == "Harrison");
var filtered = ProductDatabase.ProductDefinitions.Where(d => d.Group == group);

// Create new entry
ProductDefinition newDef = ProductDatabase.CreateProductDefinition("NewId", group);

// Save changes
DBOperationResult result = ProductDatabase.Save();
```

## Price List Operations

```csharp
// Get price list
PriceList priceList = Database.PriceLists["Nibco"];

// Product type - lookup by ID
ProductEntry entry = priceList.Entries.FirstOrDefault(e => e.Id == "004NI1234");
double price = entry.Cost;
double discount = entry.Discount;
string status = entry.Status; // A, P, or D

// Breakpoint type - lookup by row/column
// IMPORTANT: GetValue(columnIndex, rowIndex) - column first!
DBOperationResult result = priceList.GetValue(colIdx, rowIdx);
if (result.Status == ResultStatus.Succeeded)
{
    double value = (double)result.ReturnObject;
}

// Add entry
DBOperationResult addResult = priceList.AddEntry("NewProductId");
if (addResult.Status == ResultStatus.Succeeded)
{
    ProductEntry newEntry = addResult.ReturnObject as ProductEntry;
    newEntry.Cost = 99.99;
    newEntry.Discount = 0.15;
}
```

## Labor Table Operations

```csharp
// Fabrication Times (F-Rate / Shop Labor)
FabricationTimesTable fabTable = Database.FabricationTimesTables["Shop Labor"];

// Installation Times (E-Rate / Field Labor)
InstallationTimesTable installTable = Database.InstallationTimesTables["Field Install"];

// Get labor hours for product ID
ProductEntry laborEntry = fabTable.Entries.FirstOrDefault(e => e.Id == productId);
double hours = laborEntry.Time;
string units = laborEntry.Units; // "Each" or "Foot"

// Breakpoint table access (same pattern as price lists)
DBOperationResult result = table.GetValue(columnIndex, rowIndex);
```

## Item Product List Operations

```csharp
// Create template
ItemProductListDataTemplate template = new ItemProductListDataTemplate();

// Add dimensions (unlocked = user can modify)
var dimDef = item.Dimensions.FirstOrDefault(d => d.Name == "Diameter");
template.AddDimensionDefinition(
    new ItemProductListDimensionDefinition(dimDef, locked: false),
    defaultValue: 4.0);

// Add options
var optDef = item.Options.FirstOrDefault(o => o.Name == "Segments");
template.AddOptionDefinition(
    new ItemProductListOptionDefinition(optDef, locked: true),
    defaultValue: 4);

// Create product list
ItemProductList prodList = new ItemProductList();
prodList.AddDataTemplate(template);

// Add rows
List<ItemProductListDimensionEntry> dims = new List<ItemProductListDimensionEntry>
{
    template.DimensionsDefinitions[0].CreateDimensionEntry(4.0),
    template.DimensionsDefinitions[1].CreateDimensionEntry(2.0)
};

prodList.AddRow(
    name: "4x2",
    alias: "4x2-Red",
    supplier: null,
    material: null,
    specification: "",
    orderCode: "ORD-4x2",
    databaseId: "DB-4x2",
    active: true,
    flowData: null,
    boughtOutData: null,
    dimensions: dims,
    options: null);

// Apply to item
DBOperationResult result = ContentManager.CreateProductItem(item, prodList);
```

## Service Operations

```csharp
// Get all services
var services = Database.Services;

foreach (Service svc in services)
{
    string name = svc.Name;

    // Service types (sub-categories)
    foreach (ServiceType type in svc.Types)
    {
        string typeName = type.Name;
        int index = type.Index;
    }
}
```

## Common Patterns

### Handling DBOperationResult

```csharp
DBOperationResult result = SomeOperation();

switch (result.Status)
{
    case ResultStatus.Succeeded:
        var data = result.ReturnObject;
        // Process success
        break;

    case ResultStatus.Failed:
        string error = result.Message;
        MessageBox.Show(error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        break;
}
```

### Export to CSV

```csharp
// Using your existing CsvExportService pattern
var exportService = new CsvExportService();
var data = new List<string[]>();

// Header
data.Add(new[] { "ID", "Supplier", "Description", "Cost" });

// Data rows
foreach (var entry in priceList.Entries)
{
    data.Add(new[] { entry.Id, entry.Supplier, entry.Description, entry.Cost.ToString() });
}

exportService.Export(filePath, data);
```

### Import from CSV

```csharp
// Using your existing CsvImportService pattern
var importService = new CsvImportService();
var records = importService.Import<PriceImportRecord>(filePath);

foreach (var record in records)
{
    var entry = priceList.Entries.FirstOrDefault(e => e.Id == record.Id);
    if (entry != null)
    {
        entry.Cost = record.Cost;
        entry.Discount = record.Discount;
    }
}
```

## Key Gotchas

1. **BreakPointTable.GetValue()** takes `(columnIndex, rowIndex)` - column first!
2. **InstallationRate** inherits from **LabourRate**
3. **Database.InstallationTimesTable** returns collections, not single objects
4. Always check `DBOperationResult.Status` before accessing `ReturnObject`
5. Call `ProductDatabase.Save()` after modifications

## MAP Term Translations

| MAP | Fabrication API | Notes |
|-----|-----------------|-------|
| MAPProd | ProductDatabase | Product metadata |
| Harrison Code | ProductEntry.Id | Unique identifier |
| F-Rate | FabricationTimesTable | Shop labor |
| E-Rate | InstallationTimesTable | Field labor |
| M-Rate | PriceList | Material pricing |
| Service Type | ServiceType | Report category |
| Product List | ItemProductList | Size catalog |
