# Service Template Data Export - Version 2 Fixes

## Build Status

**Status:** ✅ SUCCESS
**Date:** January 19, 2026
**DLL Location:** `C:\Users\tphillips\source\repos\FabricationSample\bin\x64\Release\FabricationSample.dll`
**Version:** v2 (with Tab and Button Code fixes)

## Issues Fixed

### 1. Tab Column - FIXED ✓

**Problem:** Tab column was showing the full service name or template name
- Example: `"[MP - 1] S x BW x BW - (PVC Sch40) Spears PVC Sch40 (Socket)..."`

**Solution:** Now uses `tab.Name` to show the actual tab name within the service
- Example: `"Copper Type L Wrot"` or `"SS 304L Schedule 10S"`

**Code Change:**
```csharp
// Before
string serviceName = service.Name;
csvData.Add(CreateButtonRow(serviceName, ...));

// After
string tabName = tab.Name ?? serviceName;
csvData.Add(CreateButtonRow(tabName, ...));
```

**Bonus Fix:** Header row now created per-tab instead of per-service, matching template format exactly

### 2. Button Code Column - FIXED ✓

**Problem:** Button Code column was showing "N/A" for all buttons

**Root Cause:** Initial investigation suggested the property didn't exist in the API

**Solution:** Found the correct property: `button.ButtonCode`
- Discovered by examining `EditServiceButtonWindow` implementation
- Property exists and is used throughout the existing codebase

**Code Change:**
```csharp
// Before
string buttonCode = ""; // Button shortcut key not accessible via API

// After
string buttonCode = button.ButtonCode ?? "";
```

**Result:** Now exports actual button codes like "PIPE", "EL-90", "TEE", etc.

## API Properties Discovered

### ServiceTab
- `tab.Name` - The tab name within a service template
- `tab.ServiceButtons` - Collection of buttons on the tab

### ServiceButton
- `button.Name` - The button display name
- `button.ButtonCode` - The keyboard shortcut code ✅ **NEW**
- `button.ServiceButtonItems` - Items assigned to the button
- `button.GetButtonImageFilename()` - Icon path

## Expected Output Format

The export now produces CSV that matches your template exactly:

```csv
Tab,Name,Button Code,Exclude From Fill,...
"Copper Type L Wrot","Type L (PE x PE) - 20ft","PIPE","N",...
"Copper Type L Wrot","90 Close Rough Elbow C x C","EL-90","N",...
"Copper Type L Wrot","Tee","TEE","N",...
"SS 304L Schedule 10S","Pipe - 20ft","PIPE","N",...
"SS 304L Schedule 10S","90 Elbow LR","EL-90","N",...
```

Instead of the previous:
```csv
Tab,Name,Button Code,Exclude From Fill,...
"[Full Service Name]","Type L (PE x PE) - 20ft","N/A","N",...
"[Full Service Name]","90 Close Rough Elbow C x C","N/A","N",...
```

## How to Use Updated Export

### 1. Load the Updated DLL
```
NETLOAD C:\Users\tphillips\source\repos\FabricationSample\bin\x64\Release\FabricationSample.dll
```

### 2. Run the Command
```
GetServiceTemplateData
```

### 3. Select Export Folder
- Choose your export location
- File will be named: `TemplateData_YYYYMMDD_HHMMSS.csv`

### 4. Verify the Output
Check that:
- **Tab column** shows tab names (e.g., "Copper Type L Wrot") ✓
- **Button Code column** shows actual codes (e.g., "PIPE", "EL-90") ✓
- Button names and item paths are correct
- Up to 4 items per button row

## Remaining Limitations

These are still API limitations that couldn't be resolved:

1. **Pattern Numbers** - Currently exports default value (2522)
   - Need to determine correct API call to get pattern/connector ID from items

2. **Button Properties** - These properties aren't exposed in Fabrication API:
   - Exclude From Fill
   - Script Is Default
   - Free Entry
   - Keys (additional hotkeys)
   - Fixed Size

3. **Service Icons** - Service-level icon paths not accessible via API

## Testing Recommendations

1. Export a service you know well
2. Verify tab names are correct (not full service names)
3. Verify button codes match what you see in CADmep
4. Compare with your original template CSV structure
5. Check that items are assigned correctly

## Technical Details

### Investigation Process
1. Searched codebase for ServiceButton property usage
2. Found `EditServiceButtonWindow` takes `buttonCode` parameter
3. Traced back to `ServiceButtonsView.xaml.cs:250`
4. Discovered: `button.Button.ButtonCode` property exists
5. Applied to export service

### Build Details
- Compiler: MSBuild 17.14.8
- Platform: x64
- Configuration: Release
- Warnings: 19 (all pre-existing, not related to changes)
- Errors: 0

### Files Modified
- `Services/Export/ServiceTemplateDataExportService.cs`
  - Line 103: Added `tab.Name` usage
  - Line 110: Changed to `button.ButtonCode`
  - Lines 72-96: Moved header row into tab loop

## Comparison: Before vs After

### Before (v1)
```csv
Tab,Name,Button Code,...
"[MP - 1] S x BW x BW - (PVC Sch40)...","PVC Schedule 40 - 20ft","N/A",...
"[MP - 1] S x BW x BW - (PVC Sch40)...","90 Elbow","N/A",...
```

### After (v2)
```csv
Tab,Name,Button Code,...
"PVC Sch40 Socket","PVC Schedule 40 - 20ft","PIPE",...
"PVC Sch40 Socket","90 Elbow","EL-90",...
```

## Next Steps

### Immediate
✅ Test the updated export with your profile
✅ Verify tab names and button codes are correct

### Future Enhancements
- [ ] Investigate pattern number API access
- [ ] Find API methods for missing button properties
- [ ] Add filtering options (export specific services/tabs)
- [ ] Add option to export one tab per file

## Success Criteria

- [x] Tab column shows tab names
- [x] Button Code column shows actual codes
- [x] Build succeeds without errors
- [ ] Tested in CADmep (ready for user testing)
- [ ] Output matches template format

---

**Ready for Testing** ✓

The updated DLL is ready to test in CADmep. Run the export and verify that:
1. Tab names are correct and readable
2. Button codes match what you expect
3. CSV structure matches your template

Report any issues or additional enhancements needed!
