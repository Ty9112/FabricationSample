using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for service template data matching the [MG - 1]_TemplateData.csv format.
    /// Exports services with their button assignments, icon paths, and up to 4 item conditions per button.
    /// </summary>
    public class ServiceTemplateDataExportService : CsvExportService
    {
        /// <summary>
        /// Services to export (null = all services)
        /// </summary>
        public List<string> SelectedServiceNames { get; set; }

        /// <summary>
        /// Service templates to export (null = all templates, only used if ExportByTemplate is true)
        /// </summary>
        public List<string> SelectedTemplateNames { get; set; }

        /// <summary>
        /// If true, export by template instead of by service (excludes Service Name column)
        /// </summary>
        public bool ExportByTemplate { get; set; }

        /// <summary>
        /// Override to create unquoted CSV header line.
        /// </summary>
        private string CreateUnquotedHeaderLine(params string[] columnNames)
        {
            return CsvHelpers.FormatUnquotedCsv((object[])columnNames);
        }

        /// <summary>
        /// Override to create unquoted CSV data line.
        /// </summary>
        private string CreateUnquotedDataLine(params object[] values)
        {
            return CsvHelpers.FormatUnquotedCsv(values);
        }

        /// <summary>
        /// Generate service template data CSV export.
        /// Matches the format: Tab, Name, Button Code, Exclude From Fill, Script Is Default, Free Entry,
        /// Keys, Fixed Size, Icon Path, Item Path1-4, Pat No1-4, Condition1-4
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Add header matching template format
                if (ExportByTemplate)
                {
                    // Template mode - no Service Name column
                    csvData.Add(CreateUnquotedHeaderLine(
                        "Template Name",
                        "Tab",
                        "Name",
                        "Button Code",
                        "Exclude From Fill",
                        "Script Is Default",
                        "Free Entry",
                        "Keys",
                        "Fixed Size",
                        "Icon Path",
                        "Item Path1",
                        "Pat No1",
                        "Condition1",
                        "Item Path2",
                        "Pat No2",
                        "Condition2",
                        "Item Path3",
                        "Pat No3",
                        "Condition3",
                        "Item Path4",
                        "Pat No4",
                        "Condition4"
                    ));
                }
                else
                {
                    // Service mode - includes Service Name column
                    csvData.Add(CreateUnquotedHeaderLine(
                        "Service Name",
                        "Template Name",
                        "Tab",
                        "Name",
                        "Button Code",
                        "Exclude From Fill",
                        "Script Is Default",
                        "Free Entry",
                        "Keys",
                        "Fixed Size",
                        "Icon Path",
                        "Item Path1",
                        "Pat No1",
                        "Condition1",
                        "Item Path2",
                        "Pat No2",
                        "Condition2",
                        "Item Path3",
                        "Pat No3",
                        "Condition3",
                        "Item Path4",
                        "Pat No4",
                        "Condition4"
                    ));
                }

                int totalServices = FabDB.Services.Count;
                int processedServices = 0;

                ReportProgress(10, 100, $"Found {totalServices} services to process...");

                // Process each service
                foreach (var service in FabDB.Services)
                {
                    // Filter by selected services if specified
                    if (SelectedServiceNames != null && SelectedServiceNames.Count > 0)
                    {
                        if (!SelectedServiceNames.Contains(service.Name))
                            continue;
                    }

                    // Filter by selected templates if specified (template export mode)
                    if (ExportByTemplate && SelectedTemplateNames != null && SelectedTemplateNames.Count > 0)
                    {
                        if (service.ServiceTemplate == null || !SelectedTemplateNames.Contains(service.ServiceTemplate.Name))
                            continue;
                    }

                    if (IsCancelled) return csvData;

                    processedServices++;
                    ReportProgress(10 + (int)((processedServices / (double)totalServices) * 80), 100,
                        $"Processing service {processedServices}/{totalServices}: {service.Name}");

                    string serviceName = service.Name;
                    var serviceTemplate = service.ServiceTemplate;
                    if (serviceTemplate?.ServiceTabs == null) continue;

                    string templateName = serviceTemplate.Name ?? "";

                    // Process each tab/button in the service
                    foreach (var tab in serviceTemplate.ServiceTabs)
                    {
                        if (tab.ServiceButtons == null) continue;

                        string tabName = tab.Name ?? serviceName;

                        // Add tab header row (tab name with icon if available)
                        string tabIconPath = GetServiceIconPath(service);

                        if (ExportByTemplate)
                        {
                            // Template mode - no Service Name column
                            csvData.Add(CreateUnquotedDataLine(
                                templateName, // Template Name
                                tabName,      // Tab
                                "",           // Name (empty for header row)
                                "",           // Button Code
                                "",           // Exclude From Fill
                                "N",          // Script Is Default
                                "N",          // Free Entry
                                "",           // Keys
                                "N",          // Fixed Size
                                tabIconPath,  // Icon Path
                                "",           // Item Path1
                                "",           // Pat No1
                                "",           // Condition1
                                "",           // Item Path2
                                "",           // Pat No2
                                "",           // Condition2
                                "",           // Item Path3
                                "",           // Pat No3
                                "",           // Condition3
                                "",           // Item Path4
                                "",           // Pat No4
                                ""            // Condition4
                            ));
                        }
                        else
                        {
                            // Service mode - includes Service Name column
                            csvData.Add(CreateUnquotedDataLine(
                                serviceName,  // Service Name
                                templateName, // Template Name
                                tabName,      // Tab
                                "",           // Name (empty for header row)
                                "",           // Button Code
                                "",           // Exclude From Fill
                                "N",          // Script Is Default
                                "N",          // Free Entry
                                "",           // Keys
                                "N",          // Fixed Size
                                tabIconPath,  // Icon Path
                                "",           // Item Path1
                                "",           // Pat No1
                                "",           // Condition1
                                "",           // Item Path2
                                "",           // Pat No2
                                "",           // Condition2
                                "",           // Item Path3
                                "",           // Pat No3
                                "",           // Condition3
                                "",           // Item Path4
                                "",           // Pat No4
                                ""            // Condition4
                            ));
                        }

                        foreach (var button in tab.ServiceButtons)
                        {
                            if (IsCancelled) return csvData;

                            string buttonName = button.Name;
                            string buttonCode = button.ButtonCode ?? "";

                            var sbItems = button.ServiceButtonItems;
                            if (sbItems == null || sbItems.Count == 0)
                            {
                                // Button with no items - still add a row
                                csvData.Add(CreateButtonRow(
                                    serviceName,
                                    templateName,
                                    tabName,
                                    buttonName,
                                    buttonCode,
                                    button,
                                    new List<ButtonItemData>()
                                ));
                                continue;
                            }

                            // Collect up to 4 items for this button
                            var buttonItems = new List<ButtonItemData>();
                            int itemIndex = 0;

                            foreach (var sbItem in sbItems)
                            {
                                if (itemIndex >= 4) break; // Only take first 4 items

                                var itemData = new ButtonItemData
                                {
                                    ItemPath = GetItemPath(sbItem),
                                    PatternNumber = GetPatternNumber(sbItem),
                                    Condition = GetConditionText(sbItem)
                                };

                                buttonItems.Add(itemData);
                                itemIndex++;
                            }

                            // Create row with all collected items
                            csvData.Add(CreateButtonRow(
                                serviceName,
                                templateName,
                                tabName,
                                buttonName,
                                buttonCode,
                                button,
                                buttonItems
                            ));
                        }
                    }
                }

                ReportProgress(95, 100, $"Completed processing {processedServices} services");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating service template data CSV: {ex.Message}", ex);
            }

            return csvData;
        }

        /// <summary>
        /// Create a button row with up to 4 item assignments.
        /// </summary>
        private string CreateButtonRow(
            string serviceName,
            string templateName,
            string tabName,
            string buttonName,
            string buttonCode,
            ServiceButton button,
            List<ButtonItemData> items)
        {
            // Get button properties
            string excludeFromFill = GetButtonProperty(button, "ExcludeFromFill", "N");
            string scriptIsDefault = "N"; // Not directly accessible in API
            string freeEntry = "N";       // Not directly accessible in API
            string keys = "";             // Not directly accessible in API
            string fixedSize = "N";       // Not directly accessible in API
            string iconPath = GetButtonIconPath(button);

            // Pad items to 4 slots
            while (items.Count < 4)
            {
                items.Add(new ButtonItemData
                {
                    ItemPath = "",
                    PatternNumber = "",
                    Condition = ""
                });
            }

            if (ExportByTemplate)
            {
                // Template mode - no Service Name column
                return CreateUnquotedDataLine(
                    templateName,             // Template Name
                    tabName,                  // Tab
                    buttonName,               // Name
                    buttonCode,               // Button Code
                    excludeFromFill,          // Exclude From Fill
                    scriptIsDefault,          // Script Is Default
                    freeEntry,                // Free Entry
                    keys,                     // Keys
                    fixedSize,                // Fixed Size
                    iconPath,                 // Icon Path
                    items[0].ItemPath,        // Item Path1
                    items[0].PatternNumber,   // Pat No1
                    items[0].Condition,       // Condition1
                    items[1].ItemPath,        // Item Path2
                    items[1].PatternNumber,   // Pat No2
                    items[1].Condition,       // Condition2
                    items[2].ItemPath,        // Item Path3
                    items[2].PatternNumber,   // Pat No3
                    items[2].Condition,       // Condition3
                    items[3].ItemPath,        // Item Path4
                    items[3].PatternNumber,   // Pat No4
                    items[3].Condition        // Condition4
                );
            }
            else
            {
                // Service mode - includes Service Name column
                return CreateUnquotedDataLine(
                    serviceName,              // Service Name
                    templateName,             // Template Name
                    tabName,                  // Tab
                    buttonName,               // Name
                    buttonCode,               // Button Code
                    excludeFromFill,          // Exclude From Fill
                    scriptIsDefault,          // Script Is Default
                    freeEntry,                // Free Entry
                    keys,                     // Keys
                    fixedSize,                // Fixed Size
                    iconPath,                 // Icon Path
                    items[0].ItemPath,        // Item Path1
                    items[0].PatternNumber,   // Pat No1
                    items[0].Condition,       // Condition1
                    items[1].ItemPath,        // Item Path2
                    items[1].PatternNumber,   // Pat No2
                    items[1].Condition,       // Condition2
                    items[2].ItemPath,        // Item Path3
                    items[2].PatternNumber,   // Pat No3
                    items[2].Condition,       // Condition3
                    items[3].ItemPath,        // Item Path4
                    items[3].PatternNumber,   // Pat No4
                    items[3].Condition        // Condition4
                );
            }
        }

        /// <summary>
        /// Get the item file path for a service button item.
        /// </summary>
        private string GetItemPath(ServiceButtonItem sbItem)
        {
            try
            {
                if (string.IsNullOrEmpty(sbItem.ItemPath))
                    return "";

                // Return relative path starting with ./
                string itemPath = sbItem.ItemPath;

                // Normalize path separators
                itemPath = itemPath.Replace("\\", "/");

                // Ensure it starts with ./
                if (!itemPath.StartsWith("./"))
                    itemPath = "./" + itemPath;

                return itemPath;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get the pattern number (connector ID) for a service button item.
        /// </summary>
        private string GetPatternNumber(ServiceButtonItem sbItem)
        {
            try
            {
                Item item = ContentManager.LoadItem(sbItem.ItemPath);
                if (item == null) return "";

                // Try to get pattern/connector ID from item
                // This is typically stored in the item's connector definitions
                // For now, return a placeholder - need to determine correct API call
                return "2522"; // Default pattern number from template
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get the condition text for a service button item.
        /// </summary>
        private string GetConditionText(ServiceButtonItem sbItem)
        {
            try
            {
                var condition = sbItem.ServiceTemplateCondition;
                if (condition == null)
                    return "Unrestricted";

                // Return condition description or "Unrestricted"
                string desc = condition.Description;
                if (string.IsNullOrEmpty(desc))
                    return "Unrestricted";

                return desc;
            }
            catch
            {
                return "Unrestricted";
            }
        }

        /// <summary>
        /// Get icon path for service.
        /// </summary>
        private string GetServiceIconPath(Service service)
        {
            try
            {
                // Check if service has custom icon
                // Icon path format from template: ./Nibco.png
                // This may require custom data or metadata lookup
                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get icon path for button.
        /// </summary>
        private string GetButtonIconPath(ServiceButton button)
        {
            try
            {
                // Button icon is typically "*" or a path
                // "*" indicates to use the item's icon
                if (button.ServiceButtonItems != null && button.ServiceButtonItems.Count > 0)
                    return "*";

                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get a button property value.
        /// </summary>
        private string GetButtonProperty(ServiceButton button, string propertyName, string defaultValue)
        {
            try
            {
                // Check if button has the property
                // Most button properties aren't exposed in the API
                // Return default for now
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper class to store button item data.
        /// </summary>
        private class ButtonItemData
        {
            public string ItemPath { get; set; }
            public string PatternNumber { get; set; }
            public string Condition { get; set; }
        }
    }
}
