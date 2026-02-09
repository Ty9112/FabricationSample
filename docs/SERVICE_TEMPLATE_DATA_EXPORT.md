# Service Template Data Export - Button Report Generator

## Overview

This feature exports service button assignments to a CSV file matching the format of `[MG - 1]_TemplateData.csv`. It creates button reports by service or template showing which button codes are assigned to which items.

## File Created

**Location:** `FabricationSample\Services\Export\ServiceTemplateDataExportService.cs`

**Command:** `ExportCommands.cs` → `GetServiceTemplateData`

## CSV Format

The export generates a CSV with the following columns:

| Column | Description |
|--------|-------------|
| Tab | Service name (service group/template) |
| Name | Button name within the service |
| Button Code | Shortcut key assigned to button |
| Exclude From Fill | Whether button is excluded from fill operations |
| Script Is Default | Whether this is the default script button |
| Free Entry | Whether button allows free entry |
| Keys | Additional hotkeys |
| Fixed Size | Whether button is fixed size |
| Icon Path | Path to button icon (* = use item icon) |
| Item Path1 | First item file path (relative) |
| Pat No1 | Pattern/connector number for first item |
| Condition1 | Condition description (e.g., "Unrestricted") |
| Item Path2 | Second item file path |
| Pat No2 | Pattern/connector number for second item |
| Condition2 | Condition description |
| Item Path3 | Third item file path |
| Pat No3 | Pattern/connector number for third item |
| Condition3 | Condition description |
| Item Path4 | Fourth item file path |
| Pat No4 | Pattern/connector number for fourth item |
| Condition4 | Condition description |

## How to Use

### 1. In CADmep/AutoCAD

```
NETLOAD FabricationSample.dll
GetServiceTemplateData
```

### 2. Select Export Location

- Choose a folder where the CSV will be saved
- File will be named with timestamp: `TemplateData_YYYYMMDD_HHMMSS.csv`

### 3. Open the Result

- Option to open file automatically after export
- Import into Excel, Google Sheets, or other tools for analysis

## Output Example

```csv
Tab,Name,Button Code,Exclude From Fill,Script Is Default,Free Entry,Keys,Fixed Size,Icon Path,Item Path1,Pat No1,Condition1,Item Path2,Pat No2,Condition2,Item Path3,Pat No3,Condition3,Item Path4,Pat No4,Condition4
Copper Type L Wrot Clean & Bagged (Brazed),,,N,N,N,,N,./Nibco.png,,,,,,,,,,,,
Copper Type L Wrot Clean & Bagged (Brazed),Type L (PE x PE) - 20ft,PIPE,N,N,N,,N,*,./BIMrx Fabrication DBS/Pipework/(Generic)/Pipes/Copper/ASTM B88/Type L/Pipe - B819 Copper Type L (PE x PE) - 20ft.ITM,2041,Unrestricted,,,,,,,,,
Copper Type L Wrot Clean & Bagged (Brazed),90 Close Rough Elbow C x C,EL-90,N,N,N,,N,*,./BIMrx Fabrication DBS/Pipework/Nibco/Fittings/Copper/Soldered/CFOS/607-CB 90 Close Rough Elbow C x C - Wrot.itm,2097,Unrestricted,,,,,,,,,
```

## Features

### Service Grouping

- Each service starts with a header row (service name + icon)
- All buttons for that service follow underneath
- Easy to filter/sort by service name in Excel

### Button Codes

- Exports the shortcut key assigned to each button (PIPE, EL-90, TEE, etc.)
- Useful for documenting keyboard shortcuts
- Compare button codes across services

### Up to 4 Items Per Button

- Each button can have up to 4 different items assigned
- All 4 items shown on a single row
- Each item includes: Path, Pattern Number, Condition

### Conditions

- Exports condition descriptions (Unrestricted, size ranges, etc.)
- Shows which conditions apply to which items
- Helps understand when each item is used

## Use Cases

### 1. Documentation

Generate button reports for:
- Training materials
- CAD standards documents
- Quick reference guides

### 2. Comparison

Compare services across profiles:
- Export from Profile A
- Export from Profile B
- Use Excel to compare/diff

### 3. Audit

Review button assignments:
- Which buttons have items assigned?
- Which items are used in multiple services?
- Are button codes consistent?

### 4. Migration Planning

Before migrating to a new profile:
- Document current state
- Plan button reassignments
- Verify after migration

## Limitations / Known Issues

### API Limitations

Some properties aren't exposed in the Fabrication API:

- **Script Is Default** - Always exports as "N"
- **Free Entry** - Always exports as "N"
- **Keys** - Always blank (additional hotkeys not accessible)
- **Fixed Size** - Always exports as "N"
- **Exclude From Fill** - May not be accurate

### Pattern Numbers

- Currently exports a default value (2522)
- Need to determine correct API call to get actual pattern/connector ID from items
- May require loading each item and inspecting connector definitions

### Service Icons

- Service-level icon paths not currently exported
- Would require custom data lookup or metadata
- Button icons show "*" (use item's icon)

### More Than 4 Items

- If a button has more than 4 items, only the first 4 are exported
- Consider creating separate rows for buttons with >4 items
- Or add more columns (Item Path5-8, etc.)

## Next Steps / Enhancements

### 1. Pattern Number Lookup

Enhance `GetPatternNumber()` method to:
```csharp
Item item = ContentManager.LoadItem(sbItem.ItemPath);
// Look up actual connector/pattern ID from item
int patternId = item.??? // Determine correct property
return patternId.ToString();
```

### 2. Additional Columns

Add more data columns:
- Tab name (if different from service name)
- Button description
- Item description/alias
- Material type
- Specification group

### 3. Multiple Rows for >4 Items

Instead of truncating at 4 items, create additional rows:
- Row 1: Items 1-4
- Row 2: Items 5-8
- Etc.

### 4. Filter Options

Add export options:
- Export specific services only
- Export only buttons with items
- Export only buttons with button codes

### 5. Icon Path Resolution

Look up actual icon files:
- Service-level icons
- Button-level custom icons
- Item-level icons

## Comparison with ExportItemData

This export differs from the existing `ExportItemData` command:

| Feature | ServiceTemplateData | ItemData |
|---------|---------------------|----------|
| Format | Wide (up to 4 items per row) | Long (1 product list entry per row) |
| Focus | Button assignments | Product list details |
| Use case | Button reports, documentation | Pricing, product analysis |
| Matches template | Yes ([MG - 1]_TemplateData.csv) | No |
| Product list | Not included | Included |
| Button codes | Included | Included |
| Conditions | Up to 4 per row | 1 per row |

## Integration with Profile Copy Feature

This export can work with the Profile Copy feature:

1. **Export services** from source profile → CSV
2. **Review/modify** CSV in Excel
3. **Import services** into target profile (future enhancement)

This would enable:
- Bulk button reassignments
- Service standardization
- Profile templates

## Files Modified

1. **Created:** `Services/Export/ServiceTemplateDataExportService.cs`
2. **Modified:** `Commands/ExportCommands.cs` - Added `GetServiceTemplateData` command

## Testing

To test the export:

1. NETLOAD the DLL in CADmep
2. Run `GetServiceTemplateData`
3. Select export folder
4. Open resulting CSV
5. Verify format matches `[MG - 1]_TemplateData.csv`
6. Check that:
   - Service names appear as Tab
   - Button codes are correct
   - Item paths are relative and start with ./
   - Conditions show "Unrestricted" or actual ranges
   - Up to 4 items per row

## Support

For issues or questions:
- Check that Fabrication API is loaded (job must be open)
- Verify services exist in current profile
- Review error log at: `%LOCALAPPDATA%\FabricationSample\errors.log`

---

**Created:** January 19, 2026
**Reference:** `[MG - 1]_TemplateData.csv` (Harris Wetside Database 2_0)
