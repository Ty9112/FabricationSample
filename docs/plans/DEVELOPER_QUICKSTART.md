# Developer Quick Start Guide - Enhanced FabricationSample

## Getting Started

This guide provides developers with everything needed to start implementing Phase 2 features.

---

## Prerequisites

### Required Software
- Visual Studio 2019 or later
- AutoCAD 2024 with Fabrication CADmep 2024
- .NET Framework 4.8 SDK
- Git for version control

### Required Knowledge
- C# and .NET Framework
- WPF/XAML basics
- Autodesk Fabrication API fundamentals
- CSV file format

### Recommended Tools
- NUnit or MSTest for unit testing
- ReSharper or similar for code analysis
- Beyond Compare for file comparison

---

## Repository Setup

### 1. Clone or Access Repositories

```bash
# Repositories already exist at:
cd C:\Users\tphillips\source\repos

# DiscordCADmep - Export functionality reference
cd DiscordCADmep

# FabricationSample - Main development target
cd FabricationSample
```

### 2. Create Development Branch

```bash
cd C:\Users\tphillips\source\repos\FabricationSample
git checkout -b feature/export-import-enhancement
```

### 3. Build and Test Existing Code

```bash
# Open solution
start FabricationSample.sln

# Build in Visual Studio (Ctrl+Shift+B)
# Or command line:
msbuild FabricationSample.sln /p:Configuration=Debug /p:Platform=x64
```

### 4. Test NETLOAD

```
1. Start AutoCAD 2024
2. Load Fabrication CADmep
3. Open a test fabrication job
4. Type: NETLOAD
5. Browse to: C:\Users\tphillips\source\repos\FabricationSample\bin\x64\Debug\FabricationSample.dll
6. Type: FabAPI
7. Verify UI opens correctly
```

---

## Phase 1: Foundation Implementation

### Step 1: Create Folder Structure

```
FabricationSample/
├── Commands/              [CREATE NEW]
├── Services/              [CREATE NEW]
│   ├── Export/
│   └── Import/
└── Utilities/             [CREATE NEW]
```

**PowerShell Commands**:
```powershell
cd C:\Users\tphillips\source\repos\FabricationSample

# Create directories
New-Item -ItemType Directory -Path "Commands"
New-Item -ItemType Directory -Path "Services\Export"
New-Item -ItemType Directory -Path "Services\Import"
New-Item -ItemType Directory -Path "Utilities"
```

### Step 2: Create CSV Utilities

**File**: `Utilities/CsvHelpers.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// CSV formatting and parsing utilities
    /// Based on DiscordCADmep StringExtensions
    /// </summary>
    public static class CsvHelpers
    {
        /// <summary>
        /// Wrap a single value for CSV output
        /// Handles quotes, commas, newlines
        /// </summary>
        public static string WrapForCsv(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"N/A\"";

            // Escape quotes by doubling them (CSV standard)
            string escaped = value.Replace("\"", "\"\"");

            // Always quote to handle commas, newlines, quotes
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Wrap multiple values for CSV output
        /// Joins with commas
        /// </summary>
        public static string WrapForCsv(params object[] values)
        {
            if (values == null || values.Length == 0)
                return "\"N/A\"";

            return string.Join(",", values.Select(v =>
                (v?.ToString() ?? "N/A").WrapForCsv()));
        }

        /// <summary>
        /// Wrap enumerable collection for CSV output
        /// </summary>
        public static string WrapForCsv(this IEnumerable<string> values)
        {
            if (values == null || !values.Any())
                return "\"N/A\"";

            return string.Join(",", values.Select(v =>
                (v ?? "N/A").WrapForCsv()));
        }

        /// <summary>
        /// Parse a CSV line into fields
        /// Handles quoted values with embedded commas
        /// </summary>
        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentField.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // Field separator
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add final field
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// Unwrap CSV value (remove quotes, unescape)
        /// </summary>
        public static string UnwrapCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string trimmed = value.Trim();

            // Remove surrounding quotes
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            // Unescape doubled quotes
            return trimmed.Replace("\"\"", "\"");
        }
    }
}
```

**Add to Project**:
1. Right-click `FabricationSample` project
2. Add > Existing Item
3. Select `Utilities/CsvHelpers.cs`

### Step 3: Create Command Infrastructure

**File**: `Commands/ExportCommands.cs`

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using CADapp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace FabricationSample.Commands
{
    /// <summary>
    /// NETLOAD export commands
    /// Ported from DiscordCADmep with service layer architecture
    /// </summary>
    public class ExportCommands
    {
        // TODO: Implement commands in Phase 2

        /// <summary>
        /// Write message to AutoCAD command line
        /// </summary>
        private static void Princ(string message)
        {
            try
            {
                CADapp.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n{message}");
            }
            catch
            {
                // Silently fail if command line not available
            }
        }

        /// <summary>
        /// Show error message
        /// </summary>
        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Princ($"ERROR: {message}");
        }

        /// <summary>
        /// Show success message and optionally open file
        /// </summary>
        private static void ShowSuccess(string filePath, int rowCount = 0)
        {
            string message = rowCount > 0
                ? $"Export complete: {filePath}\n\n{rowCount} rows exported.\n\nOpen file?"
                : $"Export complete: {filePath}\n\nOpen file?";

            if (MessageBox.Show(message, "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try
                {
                    Process.Start("explorer.exe", filePath);
                }
                catch (Exception ex)
                {
                    ShowError($"Could not open file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Validate Fabrication API is loaded
        /// </summary>
        private static bool ValidateFabricationLoaded()
        {
            try
            {
                // Try to access fabrication database
                var services = Autodesk.Fabrication.DB.Database.Services;
                return true;
            }
            catch
            {
                MessageBox.Show(
                    "Fabrication API is not loaded.\n\nPlease load CADmep and open a valid fabrication job.",
                    "Fabrication API Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Prompt for export folder
        /// </summary>
        private static string PromptForExportLocation(string exportType)
        {
            try
            {
                string defaultFolder = GetDefaultExportFolder();

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = $"Select output folder for {exportType}";
                    dialog.ShowNewFolderButton = true;
                    dialog.SelectedPath = defaultFolder;

                    if (dialog.ShowDialog() == DialogResult.OK)
                        return dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error selecting folder: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get default export folder
        /// </summary>
        private static string GetDefaultExportFolder()
        {
            try
            {
                string workingDir = Autodesk.Fabrication.ApplicationServices.Application.WorkingDirectory;
                return Path.GetFullPath(Path.GetDirectoryName(workingDir));
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>
        /// Generate timestamped filename
        /// </summary>
        private static string GenerateTimestampedPath(string baseFolder, string baseName, string extension = ".csv")
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(baseFolder, $"{baseName}_{timestamp}{extension}");
        }
    }
}
```

### Step 4: Update Sample.cs

**File**: `Sample.cs` (modify existing)

Add at the top with other using statements:
```csharp
using FabricationSample.Commands;
using FabricationSample.Utilities;
```

No other changes needed in Sample.cs for Phase 1.

### Step 5: Build and Verify

```
1. Build solution (should succeed with no errors)
2. NETLOAD the DLL in AutoCAD
3. Verify existing FabAPI command still works
4. Command structure is ready for Phase 2
```

---

## Phase 2: Implementing First Export Command

### Example: ExportItemData

**Reference**: `DiscordCADmep\INI.cs` lines 34-97

**File**: `Commands/ExportCommands.cs`

```csharp
[CommandMethod("ExportItemData")]
public static void ExportItemData()
{
    try
    {
        // 1. Validate environment
        if (!ValidateFabricationLoaded())
            return;

        // 2. Get export location
        string exportFolder = PromptForExportLocation("Item Data");
        if (string.IsNullOrEmpty(exportFolder))
            return; // User cancelled

        // 3. Generate output path
        string exportPath = GenerateTimestampedPath(exportFolder, "ItemReport");

        // 4. Build CSV data
        Princ("Exporting item data...");
        var csvLines = new List<string>
        {
            CsvHelpers.WrapForCsv("ServiceName", "ServiceTemplate", "ButtonName",
                                   "ItemFilePath", "ProductListEntryName",
                                   "ConditionDescription", "GreaterThan", "Id", "LessThanEqualTo")
        };

        int itemCount = 0;

        foreach (var service in Autodesk.Fabrication.DB.Database.Services)
        {
            var serviceName = service.Name;
            var serviceTemplate = service.ServiceTemplate;
            if (serviceTemplate == null) continue;

            string templateName = serviceTemplate.Name;
            var serviceTabs = serviceTemplate.ServiceTabs;
            if (serviceTabs == null) continue;

            foreach (var tab in serviceTabs)
            {
                var buttons = tab.ServiceButtons;
                if (buttons == null) continue;

                foreach (var button in buttons)
                {
                    string buttonName = button.Name;
                    var sbItems = button.ServiceButtonItems;
                    if (sbItems == null) continue;

                    foreach (var sbItem in sbItems)
                    {
                        Item item = null;
                        try
                        {
                            item = Autodesk.Fabrication.Content.ContentManager.LoadItem(sbItem.ItemPath);
                        }
                        catch { }

                        string itemPath = item != null ? item.FilePath : "";

                        if (item != null && item.ProductList != null && item.ProductList.Rows != null)
                        {
                            var condition = sbItem.ServiceTemplateCondition;
                            string conditionDesc = condition != null ? condition.Description : "";
                            string greaterThan = condition != null ? (condition.GreaterThan > -1 ? condition.GreaterThan.ToString() : "Unrestricted") : "N/A";
                            string id = condition != null ? condition.Id.ToString() : "N/A";
                            string lessThanEqualTo = condition != null ? (condition.LessThanEqualTo > -1 ? condition.LessThanEqualTo.ToString() : "Unrestricted") : "N/A";

                            foreach (var plRow in item.ProductList.Rows)
                            {
                                string entryName = "";
                                try { entryName = plRow.Name; } catch { }

                                csvLines.Add(CsvHelpers.WrapForCsv(serviceName, templateName, buttonName,
                                                                     itemPath, entryName, conditionDesc,
                                                                     greaterThan, id, lessThanEqualTo));
                                itemCount++;
                            }
                        }
                        else
                        {
                            csvLines.Add(CsvHelpers.WrapForCsv(serviceName, templateName, buttonName,
                                                                 itemPath, "N/A", "N/A", "N/A", "N/A", "N/A"));
                            itemCount++;
                        }
                    }
                }
            }
        }

        // 5. Write to file
        File.WriteAllLines(exportPath, csvLines);

        // 6. Show success
        ShowSuccess(exportPath, itemCount);
        Princ($"Exported {itemCount} items to {exportPath}");
    }
    catch (Exception ex)
    {
        ShowError($"Export failed: {ex.Message}");
    }
}
```

### Testing the Export

```
1. Build solution
2. NETLOAD in AutoCAD
3. Type: ExportItemData
4. Select output folder
5. Verify CSV is created
6. Open in Excel and verify data
```

---

## Common Patterns

### Pattern 1: Accessing Fabrication Database

```csharp
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

// Services
foreach (var service in FabDB.Services)
{
    var serviceName = service.Name;
    var serviceTemplate = service.ServiceTemplate;
}

// Price Lists
foreach (var group in FabDB.SupplierGroups)
{
    foreach (var list in group.PriceLists)
    {
        if (list is PriceList simpleList)
        {
            foreach (var entry in simpleList.Products)
            {
                var id = entry.DatabaseId;
                var cost = entry.Value;
            }
        }
        else if (list is PriceListWithBreakPoints bpList)
        {
            var table = bpList.DefaultTable;
            // Access breakpoint table
        }
    }
}

// Installation Times
foreach (var table in FabDB.InstallationTimesTable)
{
    var tableName = table.Name;
    var tableGroup = table.Group;

    if (table is InstallationTimesTable simpleTable)
    {
        foreach (var entry in simpleTable.Products)
        {
            var id = entry.DatabaseId;
            var laborRate = entry.Value;
        }
    }
    else if (table is InstallationTimesTableWithBreakpoints bpTable)
    {
        var bpTableData = bpTable.Table;
        // Access breakpoint table
    }
}
```

### Pattern 2: Loading Items

```csharp
using Autodesk.Fabrication.Content;

// Load item from path
Item item = ContentManager.LoadItem(itemPath);

// Access product list
if (item.IsProductList && item.ProductList != null)
{
    foreach (var row in item.ProductList.Rows)
    {
        string name = row.Name;
        string dbId = row.DatabaseId;

        // Access dimensions
        foreach (var dim in row.Dimensions)
        {
            string dimName = dim.Definition.Name;
            double dimValue = dim.Value;
        }

        // Access options
        foreach (var opt in row.Options)
        {
            string optName = opt.Definition.Name;
            double optValue = opt.Value;
        }
    }
}
```

### Pattern 3: Progress Reporting

```csharp
// In AutoCAD command line
private static void ReportProgress(int current, int total, string action)
{
    if (current % 100 == 0) // Report every 100 items
    {
        Princ($"  {action}: {current}/{total} ({(current * 100 / total)}%)");
    }
}

// Usage
foreach (var item in items)
{
    ProcessItem(item);
    itemCount++;
    ReportProgress(itemCount, totalItems, "Exporting items");
}
```

### Pattern 4: Error Handling

```csharp
[CommandMethod("ExportData")]
public static void ExportData()
{
    try
    {
        // Validate
        if (!ValidateFabricationLoaded())
            return;

        // Main logic
        // ...
    }
    catch (UnauthorizedAccessException ex)
    {
        ShowError($"Access denied: {ex.Message}\n\nCheck file/folder permissions.");
    }
    catch (IOException ex)
    {
        ShowError($"File I/O error: {ex.Message}\n\nFile may be open in another program.");
    }
    catch (Exception ex)
    {
        ShowError($"Unexpected error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
    }
}
```

---

## Debugging Tips

### 1. Attach Debugger to AutoCAD

```
1. Start AutoCAD 2024
2. NETLOAD your DLL
3. In Visual Studio: Debug > Attach to Process
4. Select "acad.exe"
5. Set breakpoints in your code
6. Run commands in AutoCAD
```

### 2. Debug Output

```csharp
// Add to commands for debugging
System.Diagnostics.Debug.WriteLine($"Processing item: {itemName}");
```

View output in Visual Studio Output window (Debug mode only).

### 3. Log to File

```csharp
private static void LogError(string command, Exception ex)
{
    try
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FabricationSample",
            "errors.log"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(logPath));

        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {command}: {ex.Message}\n{ex.StackTrace}\n\n";
        File.AppendAllText(logPath, logEntry);
    }
    catch { }
}
```

### 4. Test with Small Datasets

Create a minimal fabrication job for testing:
- 1-2 services
- 5-10 items
- Simple price lists
- No breakpoint tables

This makes debugging much faster.

---

## Code Style Guidelines

### Naming Conventions

```csharp
// Commands - Verb + Noun
[CommandMethod("ExportItemData")]    // Good
[CommandMethod("ItemDataExport")]    // Avoid

// Methods - PascalCase, descriptive
private static void ShowError(string message)        // Good
private static void err(string m)                   // Avoid

// Variables - camelCase
string exportPath = "...";           // Good
string ExportPath = "...";           // Avoid

// Constants - UPPER_CASE
private const string DEFAULT_EXTENSION = ".csv";     // Good
```

### Comments

```csharp
/// <summary>
/// Exports item data from services to CSV
/// </summary>
/// <remarks>
/// CSV format: ServiceName, ServiceTemplate, ButtonName, ItemFilePath, ...
/// Multi-row output for items with product lists
/// </remarks>
[CommandMethod("ExportItemData")]
public static void ExportItemData()
{
    // Comments for complex logic only
    // Self-documenting code is preferred
}
```

### Error Messages

```csharp
// User-friendly messages
ShowError("Could not access fabrication database. Please ensure CADmep is loaded.");

// Developer-friendly messages (for logs)
LogError("ExportItemData", new Exception($"Database access failed at service index {i}"));
```

---

## Testing Checklist

### Before Committing Code

- [ ] Code builds without warnings
- [ ] Existing FabAPI command still works
- [ ] New command produces expected CSV output
- [ ] CSV opens correctly in Excel
- [ ] Error cases handled gracefully
- [ ] No memory leaks (test with large datasets)
- [ ] Code follows style guidelines
- [ ] XML documentation complete

### Integration Testing

- [ ] Test with empty database
- [ ] Test with minimal database
- [ ] Test with production-size database (5000+ products)
- [ ] Test with special characters in names (commas, quotes, newlines)
- [ ] Test with read-only output folder (should show error)
- [ ] Test cancellation during long operations

---

## Getting Help

### Resources

1. **Implementation Plan**: `PHASE2_IMPLEMENTATION_PLAN.md` - Complete specifications
2. **Architecture**: `ARCHITECTURE_DIAGRAM.md` - System design and data flow
3. **Reference Code**: `DiscordCADmep\INI.cs` - Working export examples
4. **API Documentation**: `FabricationAPI.chm` - Autodesk API reference

### Common Issues

**Issue**: "FabricationAPI.dll not found"
**Solution**: Ensure AutoCAD 2024 and CADmep are installed. Check DLL path in project references.

**Issue**: "NETLOAD fails with BadImageFormatException"
**Solution**: Ensure platform target is x64, not AnyCPU or x86.

**Issue**: "Command not found after NETLOAD"
**Solution**: Check [CommandMethod] attribute spelling. Rebuild solution. Try NETLOAD again.

**Issue**: "Access denied writing CSV"
**Solution**: Check file permissions. Ensure file isn't open in Excel. Try different folder.

---

## Next Steps

1. **Complete Phase 1** - Foundation (this document)
2. **Implement remaining export commands** - One at a time, test each
3. **Create service layer** - Refactor commands to use services
4. **Add UI integration** - Export buttons in DatabaseEditor
5. **Implement import features** - CSV import with validation
6. **Polish and test** - Comprehensive testing and documentation

---

**Document Version**: 1.0
**Date**: 2026-01-09
**For Questions**: Review implementation plan or architecture docs first
