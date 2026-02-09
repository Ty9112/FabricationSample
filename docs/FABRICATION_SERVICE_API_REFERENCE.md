# Autodesk Fabrication Service API Reference

Complete documentation of all accessible properties and data from the Autodesk.Fabrication.DB service classes.

**Last Updated:** January 21, 2026
**API Version:** Autodesk.Fabrication 2025+
**Based on:** FabricationSample codebase analysis

---

## Table of Contents

1. [Overview](#overview)
2. [Database Access](#database-access)
3. [Service Class](#service-class)
4. [ServiceEntry Class](#serviceentry-class)
5. [ServiceTemplate Class](#servicetemplate-class)
6. [ServiceTab Class](#servicetab-class)
7. [ServiceButton Class](#servicebutton-class)
8. [ServiceButtonItem Class](#servicebuttonitem-class)
9. [ServiceTemplateCondition Class](#servicetemplatecondition-class)
10. [Complete Hierarchy](#complete-hierarchy)
11. [Code Examples](#code-examples)
12. [Important Notes](#important-notes)

---

## Overview

The Autodesk.Fabrication.DB namespace provides access to fabrication service data through a hierarchical object model. Services define how items are organized, displayed, and accessed in the fabrication environment.

**Key Namespace:**
```csharp
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;
```

---

## Database Access

### Available Collections

```csharp
// Access all services
foreach (var service in FabDB.Services)
{
    // Process service
}

// Access all service templates
foreach (var template in FabDB.ServiceTemplates)
{
    // Process template
}

// Save changes
DBOperationResult result = FabDB.SaveServices();
```

**Database Methods:**
- `Database.Services` - IEnumerable<Service> of all services
- `Database.ServiceTemplates` - IEnumerable<ServiceTemplate> of all templates
- `Database.SaveServices()` - Saves service changes to database

---

## Service Class

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Id` | int | Read | Unique identifier for the service |
| `Name` | string | Read/Write | Name of the service |
| `Group` | string | Read/Write | Group classification |
| `ServiceTemplate` | ServiceTemplate | Read/Write | Associated template |
| `Specification` | Specification | Read/Write | Assigned specification (nullable) |
| `ServiceEntries` | IEnumerable<ServiceEntry> | Read | Collection of service entries |

### Methods

```csharp
// Load an item from a service button
service.LoadServiceItem(ServiceButton button, ServiceButtonItem buttonItem, bool carrySpecs);

// Add a service entry
ServiceEntry entry = service.AddServiceEntry(ServiceType serviceType);

// Delete a service entry
service.DeleteServiceEntry(ServiceEntry serviceEntry);
```

### Example Usage

```csharp
foreach (var service in FabDB.Services)
{
    Console.WriteLine($"Service: {service.Name}");
    Console.WriteLine($"  ID: {service.Id}");
    Console.WriteLine($"  Group: {service.Group}");

    if (service.ServiceTemplate != null)
    {
        Console.WriteLine($"  Template: {service.ServiceTemplate.Name}");
    }

    if (service.Specification != null)
    {
        Console.WriteLine($"  Specification: {service.Specification.Name}");
    }

    foreach (var entry in service.ServiceEntries)
    {
        Console.WriteLine($"  Entry: {entry.ServiceType.Description}");
    }
}
```

---

## ServiceEntry Class

ServiceEntry objects define configuration for specific service types within a service.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `ServiceType` | ServiceType | Read/Write | Service type configuration |
| `LayerTag1` | string | Read/Write | Primary layer tag/name |
| `LayerTag2` | string | Read/Write | Secondary layer tag/name |
| `LayerColor` | int | Read/Write | Layer color (AutoCAD Color Index 0-256) |
| `LevelBlock` | string | Read/Write | Level block identifier |
| `SizeBlock` | string | Read/Write | Size block identifier |
| `LineWeight` | LineWeight | Read | Line weight configuration |
| `IncludesInsulation` | bool | Read/Write | Insulation inclusion flag |

### ServiceType Properties

```csharp
ServiceEntry entry = service.ServiceEntries.First();
int typeId = entry.ServiceType.Id;
string typeDesc = entry.ServiceType.Description;
```

### LineWeight Properties

```csharp
int lineWeightValue = entry.LineWeight.LineWeightValue;
```

### Example Usage

```csharp
foreach (var entry in service.ServiceEntries)
{
    Console.WriteLine($"Service Type: {entry.ServiceType.Description}");
    Console.WriteLine($"  Layer 1: {entry.LayerTag1}");
    Console.WriteLine($"  Layer 2: {entry.LayerTag2}");
    Console.WriteLine($"  Layer Color: {entry.LayerColor}");
    Console.WriteLine($"  Level Block: {entry.LevelBlock}");
    Console.WriteLine($"  Size Block: {entry.SizeBlock}");
    Console.WriteLine($"  Line Weight: {entry.LineWeight.LineWeightValue}");
    Console.WriteLine($"  Includes Insulation: {entry.IncludesInsulation}");
}
```

---

## ServiceTemplate Class

ServiceTemplate objects define the structure and hierarchy of services.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Id` | int | Read | Unique identifier |
| `Name` | string | Read/Write | Template name |
| `ServiceTabs` | IEnumerable<ServiceTab> | Read | Collection of tabs |
| `Conditions` | IEnumerable<ServiceTemplateCondition> | Read | Collection of conditions |

### Methods

```csharp
// Add a service tab
ServiceTab tab = template.AddServiceTab(string name);

// Delete a service tab
template.DeleteServiceTab(ServiceTab serviceTab);

// Add a template condition
ServiceTemplateCondition condition = template.AddServiceTemplateCondition(
    string description,
    double greaterThan,
    double lessThanEqualTo
);

// Delete a template condition
template.DeleteServiceTemplateCondition(ServiceTemplateCondition condition);
```

### Example Usage

```csharp
foreach (var template in FabDB.ServiceTemplates)
{
    Console.WriteLine($"Template: {template.Name}");
    Console.WriteLine($"  ID: {template.Id}");
    Console.WriteLine($"  Tabs: {template.ServiceTabs.Count()}");
    Console.WriteLine($"  Conditions: {template.Conditions.Count()}");

    foreach (var tab in template.ServiceTabs)
    {
        Console.WriteLine($"    Tab: {tab.Name}");
    }

    foreach (var condition in template.Conditions)
    {
        Console.WriteLine($"    Condition: {condition.Description}");
    }
}
```

---

## ServiceTab Class

ServiceTab objects organize ServiceButtons into logical tabs.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Id` | int | Read | Unique identifier |
| `Name` | string | Read/Write | Tab name |
| `ServiceButtons` | IEnumerable<ServiceButton> | Read | Collection of buttons |

### Methods

```csharp
// Add a service button
ServiceButton button = tab.AddServiceButton(string name);

// Delete a service button
tab.DeleteServiceButton(ServiceButton serviceButton);
```

### Example Usage

```csharp
foreach (var tab in serviceTemplate.ServiceTabs)
{
    Console.WriteLine($"Tab: {tab.Name}");
    Console.WriteLine($"  ID: {tab.Id}");
    Console.WriteLine($"  Buttons: {tab.ServiceButtons.Count()}");

    foreach (var button in tab.ServiceButtons)
    {
        Console.WriteLine($"    Button: {button.Name}");
    }
}
```

---

## ServiceButton Class

ServiceButton objects provide named buttons within tabs that can load items.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Name` | string | Read/Write | Button name |
| `ButtonCode` | string | Read/Write | Button code identifier (nullable) |
| `ServiceButtonItems` | IEnumerable<ServiceButtonItem> | Read | Collection of items |

### Methods

```csharp
// Add an item to the button
ServiceButtonItem item = button.AddServiceButtonItem(
    string path,
    ServiceTemplateCondition templateCondition
);

// Delete an item from the button
button.DeleteServiceButtonItem(ServiceButtonItem buttonItem);

// Get button image filename
string imagePath = button.GetButtonImageFilename();
```

### Example Usage

```csharp
foreach (var button in tab.ServiceButtons)
{
    Console.WriteLine($"Button: {button.Name}");
    Console.WriteLine($"  Button Code: {button.ButtonCode ?? "None"}");
    Console.WriteLine($"  Items: {button.ServiceButtonItems.Count()}");

    string imagePath = button.GetButtonImageFilename();
    if (!string.IsNullOrEmpty(imagePath))
    {
        Console.WriteLine($"  Image: {imagePath}");
    }

    foreach (var item in button.ServiceButtonItems)
    {
        Console.WriteLine($"    Item: {item.ItemPath}");
    }
}
```

---

## ServiceButtonItem Class

ServiceButtonItem objects represent actual items that can be loaded from a button.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `ItemPath` | string | Read/Write | Path to .itm file (relative) |
| `ServiceTemplateCondition` | ServiceTemplateCondition | Read/Write | Associated condition |
| `LessThanEqualTo` | double | Read | Upper bound (use SetConditionOverride to modify) |
| `GreaterThan` | double | Read | Lower bound (use SetConditionOverride to modify) |

### Methods

```csharp
// Set condition override values
item.SetConditionOverride(double greaterThan, double lessThanEqualTo);
```

### Example Usage

```csharp
foreach (var item in button.ServiceButtonItems)
{
    Console.WriteLine($"Item Path: {item.ItemPath}");
    Console.WriteLine($"  Greater Than: {item.GreaterThan}");
    Console.WriteLine($"  Less Than/Equal: {item.LessThanEqualTo}");

    if (item.ServiceTemplateCondition != null)
    {
        Console.WriteLine($"  Condition: {item.ServiceTemplateCondition.Description}");
    }
    else
    {
        Console.WriteLine($"  Condition: Unrestricted");
    }
}
```

---

## ServiceTemplateCondition Class

ServiceTemplateCondition objects define size ranges and conditions for item selection.

### Properties

| Property | Type | Access | Description |
|----------|------|--------|-------------|
| `Id` | int | Read | Unique identifier |
| `Description` | string | Read/Write | Condition description |
| `GreaterThan` | double | Read/Write | Lower bound value |
| `LessThanEqualTo` | double | Read/Write | Upper bound value (-1 = unrestricted) |

### Methods

```csharp
// Set condition values
condition.SetConditionValues(double greaterThan, double lessThanEqualTo);
```

### Special Values

- **-1**: Represents "Unrestricted" for upper/lower bounds
- Used to indicate no size limitation

### Example Usage

```csharp
foreach (var condition in template.Conditions)
{
    Console.WriteLine($"Condition: {condition.Description}");
    Console.WriteLine($"  ID: {condition.Id}");

    if (condition.GreaterThan == -1)
    {
        Console.WriteLine($"  Lower Bound: Unrestricted");
    }
    else
    {
        Console.WriteLine($"  Lower Bound: > {condition.GreaterThan}");
    }

    if (condition.LessThanEqualTo == -1)
    {
        Console.WriteLine($"  Upper Bound: Unrestricted");
    }
    else
    {
        Console.WriteLine($"  Upper Bound: <= {condition.LessThanEqualTo}");
    }
}
```

---

## Complete Hierarchy

Visual representation of the complete service object hierarchy:

```
Database
├── Services (IEnumerable<Service>)
│   └── Service
│       ├── Id (int)
│       ├── Name (string)
│       ├── Group (string)
│       ├── ServiceTemplate (ServiceTemplate)
│       ├── Specification (Specification, nullable)
│       └── ServiceEntries (IEnumerable<ServiceEntry>)
│           └── ServiceEntry
│               ├── ServiceType
│               │   ├── Id (int)
│               │   └── Description (string)
│               ├── LayerTag1 (string)
│               ├── LayerTag2 (string)
│               ├── LayerColor (int)
│               ├── LevelBlock (string)
│               ├── SizeBlock (string)
│               ├── LineWeight
│               │   └── LineWeightValue (int)
│               └── IncludesInsulation (bool)
│
└── ServiceTemplates (IEnumerable<ServiceTemplate>)
    └── ServiceTemplate
        ├── Id (int)
        ├── Name (string)
        ├── ServiceTabs (IEnumerable<ServiceTab>)
        │   └── ServiceTab
        │       ├── Id (int)
        │       ├── Name (string)
        │       └── ServiceButtons (IEnumerable<ServiceButton>)
        │           └── ServiceButton
        │               ├── Name (string)
        │               ├── ButtonCode (string, nullable)
        │               └── ServiceButtonItems (IEnumerable<ServiceButtonItem>)
        │                   └── ServiceButtonItem
        │                       ├── ItemPath (string)
        │                       ├── GreaterThan (double)
        │                       ├── LessThanEqualTo (double)
        │                       └── ServiceTemplateCondition
        │                           ├── Id (int)
        │                           ├── Description (string)
        │                           ├── GreaterThan (double)
        │                           └── LessThanEqualTo (double)
        │
        └── Conditions (IEnumerable<ServiceTemplateCondition>)
            └── ServiceTemplateCondition
                ├── Id (int)
                ├── Description (string)
                ├── GreaterThan (double)
                └── LessThanEqualTo (double)
```

---

## Code Examples

### Example 1: Export All Service Data to Console

```csharp
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

public void ExportAllServiceData()
{
    foreach (var service in FabDB.Services)
    {
        Console.WriteLine($"\n=== SERVICE: {service.Name} ===");
        Console.WriteLine($"ID: {service.Id}");
        Console.WriteLine($"Group: {service.Group}");

        // Service Template
        if (service.ServiceTemplate != null)
        {
            var template = service.ServiceTemplate;
            Console.WriteLine($"\nTemplate: {template.Name}");

            // Tabs and Buttons
            foreach (var tab in template.ServiceTabs)
            {
                Console.WriteLine($"  Tab: {tab.Name}");

                foreach (var button in tab.ServiceButtons)
                {
                    Console.WriteLine($"    Button: {button.Name}");
                    Console.WriteLine($"      Code: {button.ButtonCode ?? "N/A"}");

                    foreach (var item in button.ServiceButtonItems)
                    {
                        Console.WriteLine($"        Item: {item.ItemPath}");

                        if (item.ServiceTemplateCondition != null)
                        {
                            Console.WriteLine($"          Condition: {item.ServiceTemplateCondition.Description}");
                        }
                    }
                }
            }

            // Conditions
            Console.WriteLine($"\n  Template Conditions:");
            foreach (var condition in template.Conditions)
            {
                Console.WriteLine($"    {condition.Description}: {condition.GreaterThan} to {condition.LessThanEqualTo}");
            }
        }

        // Service Entries (Layer Configuration)
        Console.WriteLine($"\n  Service Entries:");
        foreach (var entry in service.ServiceEntries)
        {
            Console.WriteLine($"    Type: {entry.ServiceType.Description}");
            Console.WriteLine($"      Layer 1: {entry.LayerTag1}");
            Console.WriteLine($"      Layer 2: {entry.LayerTag2}");
            Console.WriteLine($"      Color: {entry.LayerColor}");
            Console.WriteLine($"      Level Block: {entry.LevelBlock}");
            Console.WriteLine($"      Size Block: {entry.SizeBlock}");
            Console.WriteLine($"      Insulation: {entry.IncludesInsulation}");
        }
    }
}
```

### Example 2: Find All Buttons with Specific Code

```csharp
public List<ServiceButton> FindButtonsByCode(string buttonCode)
{
    var results = new List<ServiceButton>();

    foreach (var service in FabDB.Services)
    {
        if (service.ServiceTemplate?.ServiceTabs == null) continue;

        foreach (var tab in service.ServiceTemplate.ServiceTabs)
        {
            if (tab.ServiceButtons == null) continue;

            foreach (var button in tab.ServiceButtons)
            {
                if (button.ButtonCode == buttonCode)
                {
                    results.Add(button);
                    Console.WriteLine($"Found: {service.Name} > {tab.Name} > {button.Name}");
                }
            }
        }
    }

    return results;
}
```

### Example 3: Create New Service Template Programmatically

```csharp
public ServiceTemplate CreateNewServiceTemplate(string templateName)
{
    // Note: ServiceTemplate creation may require special API methods
    // This example shows the structure of working with templates

    var template = /* Get or create template */;
    template.Name = templateName;

    // Add a tab
    var tab = template.AddServiceTab("Main Tab");

    // Add a button
    var button = tab.AddServiceButton("Pipe Button");
    button.ButtonCode = "PIPE";

    // Add a condition
    var condition = template.AddServiceTemplateCondition(
        "6 inch and under",
        0.0,
        6.0
    );

    // Add an item to the button
    var item = button.AddServiceButtonItem(
        "./Pipework/Round Pipe.itm",
        condition
    );

    // Save changes
    FabDB.SaveServices();

    return template;
}
```

### Example 4: Export Service Configuration to CSV

```csharp
public void ExportServiceConfigurationToCsv(string outputPath)
{
    var csvLines = new List<string>();

    // Header
    csvLines.Add("Service,Group,Template,Tab,Button,ButtonCode,ItemPath,Condition");

    foreach (var service in FabDB.Services)
    {
        string serviceName = service.Name;
        string group = service.Group;
        string templateName = service.ServiceTemplate?.Name ?? "";

        if (service.ServiceTemplate?.ServiceTabs == null) continue;

        foreach (var tab in service.ServiceTemplate.ServiceTabs)
        {
            if (tab.ServiceButtons == null) continue;

            foreach (var button in tab.ServiceButtons)
            {
                if (button.ServiceButtonItems == null || button.ServiceButtonItems.Count() == 0)
                {
                    // Button with no items
                    csvLines.Add($"{serviceName},{group},{templateName},{tab.Name},{button.Name},{button.ButtonCode ?? ""},,");
                }
                else
                {
                    foreach (var item in button.ServiceButtonItems)
                    {
                        string condition = item.ServiceTemplateCondition?.Description ?? "Unrestricted";
                        csvLines.Add($"{serviceName},{group},{templateName},{tab.Name},{button.Name},{button.ButtonCode ?? ""},{item.ItemPath},{condition}");
                    }
                }
            }
        }
    }

    File.WriteAllLines(outputPath, csvLines);
}
```

### Example 5: Update Layer Configuration for Service Entry

```csharp
public void UpdateServiceLayerConfiguration(Service service, string serviceTypeDescription)
{
    foreach (var entry in service.ServiceEntries)
    {
        if (entry.ServiceType.Description == serviceTypeDescription)
        {
            // Update layer configuration
            entry.LayerTag1 = "HVACSupply";
            entry.LayerTag2 = "HVACDuct";
            entry.LayerColor = 150; // AutoCAD color index
            entry.LevelBlock = "LEVEL1";
            entry.SizeBlock = "SIZE12";
            entry.IncludesInsulation = true;

            Console.WriteLine($"Updated {serviceTypeDescription} layer configuration");
        }
    }

    // Save changes
    FabDB.SaveServices();
}
```

---

## Important Notes

### 1. Null Checks Required

Many properties can be null and should be checked before use:
```csharp
if (service.ServiceTemplate != null &&
    service.ServiceTemplate.ServiceTabs != null)
{
    // Safe to access
}
```

### 2. Unrestricted Values

Condition values use `-1` to represent "Unrestricted":
```csharp
if (condition.LessThanEqualTo == -1)
{
    Console.WriteLine("No upper limit");
}
```

### 3. Path Formats

ItemPath values use relative paths starting with `./`:
```csharp
string itemPath = "./Pipework/Round Pipe.itm";
```

### 4. Color System

LayerColor uses AutoCAD Color Index (0-256):
```csharp
entry.LayerColor = 150; // Specific ACI color
```

### 5. Collection Modification

Collections are read-only. Use Add/Delete methods on parent objects:
```csharp
// Correct
var tab = template.AddServiceTab("New Tab");
template.DeleteServiceTab(tab);

// Incorrect
template.ServiceTabs.Add(tab); // Won't work
```

### 6. Saving Changes

Always save after making modifications:
```csharp
// Make changes
service.Name = "Updated Name";

// Save
DBOperationResult result = FabDB.SaveServices();
if (result.Status == ResultStatus.Failed)
{
    Console.WriteLine($"Error: {result.Message}");
}
```

### 7. Properties Not Found in API

The following properties were **NOT** found in the API during analysis:
- **Rise** - No direct property for rise/fall/slope configuration
- **Fall** - No direct property for rise/fall/slope configuration
- **Design Entry** - No direct property for design entry mode
- **Service Icon Path** - No direct property (may use GetButtonImageFilename() on buttons)

These properties may be:
- Stored in custom data
- Accessed through alternative APIs
- Not exposed in the public API
- Handled at a different level (Item vs Service)

---

## Reference Files

Key files in FabricationSample codebase containing examples:

- **Data/DataMapping.cs** - Mapper classes showing property access patterns
- **Examples/FabricationAPIExamples.cs** - CRUD operation examples
- **UserControls/ServiceEditor/ServiceEditor.xaml.cs** - Real-world service manipulation
- **Services/Export/ServiceTemplateDataExportService.cs** - Comprehensive enumeration
- **UserControls/DatabaseEditor/DatabaseEditor-Services.cs** - Service loading and display
- **UserControls/DatabaseEditor/DatabaseEditor-ServiceTemplates.cs** - Template management

---

## Conclusion

This document provides complete coverage of all accessible service properties in the Autodesk.Fabrication.DB API. For additional functionality not covered here, consider:

1. Checking custom data storage mechanisms
2. Exploring item-level properties (vs service-level)
3. Using Content Manager APIs for item metadata
4. Consulting Autodesk Fabrication API documentation for updates

**For Questions or Updates:**
- Review the FabricationSample codebase examples
- Check Autodesk Fabrication API documentation
- Test property access in your environment to confirm availability

---

**Document Version:** 1.0
**Created:** January 21, 2026
**Based on:** FabricationSample comprehensive codebase analysis
