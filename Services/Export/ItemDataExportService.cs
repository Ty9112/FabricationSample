using System;
using System.Collections.Generic;
using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for item data with product list entries.
    /// Exports all service items showing their product list assignments and template conditions.
    /// </summary>
    public class ItemDataExportService : CsvExportService
    {
        /// <summary>
        /// Generate item data CSV export.
        /// Includes service name, template, button, item path, product entries, and conditions.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Add header
                csvData.Add(CreateHeaderLine(
                    "ServiceName",
                    "ServiceTemplate",
                    "ButtonName",
                    "ItemFilePath",
                    "ProductListEntryName",
                    "ConditionDescription",
                    "GreaterThan",
                    "Id",
                    "LessThanEqualTo"
                ));

                int totalItems = 0;
                int processedItems = 0;

                // First pass: count items for progress reporting
                foreach (var service in FabDB.Services)
                {
                    var serviceTemplate = service.ServiceTemplate;
                    if (serviceTemplate?.ServiceTabs == null) continue;

                    foreach (var tab in serviceTemplate.ServiceTabs)
                    {
                        if (tab.ServiceButtons == null) continue;
                        foreach (var button in tab.ServiceButtons)
                        {
                            if (button.ServiceButtonItems != null)
                                totalItems += button.ServiceButtonItems.Count;
                        }
                    }
                }

                ReportProgress(10, 100, $"Found {totalItems} items to process...");

                // Second pass: generate CSV data
                foreach (var service in FabDB.Services)
                {
                    if (IsCancelled) return csvData;

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
                                if (IsCancelled) return csvData;

                                processedItems++;
                                if (processedItems % 100 == 0)
                                {
                                    int progress = 10 + (int)((processedItems / (double)totalItems) * 80);
                                    ReportProgress(progress, 100, $"Processing items... {processedItems}/{totalItems}");
                                }

                                Item item = null;
                                try
                                {
                                    item = ContentManager.LoadItem(sbItem.ItemPath);
                                }
                                catch { }

                                string itemPath = item != null ? item.FilePath : "";

                                // Process product list entries
                                if (item != null && item.ProductList != null && item.ProductList.Rows != null)
                                {
                                    var condition = sbItem.ServiceTemplateCondition;
                                    string conditionDesc = condition != null ? condition.Description : "";
                                    string greaterThan = condition != null
                                        ? (condition.GreaterThan > -1 ? condition.GreaterThan.ToString() : "Unrestricted")
                                        : "N/A";
                                    string id = condition != null ? condition.Id.ToString() : "N/A";
                                    string lessThanEqualTo = condition != null
                                        ? (condition.LessThanEqualTo > -1 ? condition.LessThanEqualTo.ToString() : "Unrestricted")
                                        : "N/A";

                                    foreach (var plRow in item.ProductList.Rows)
                                    {
                                        string entryName = "";
                                        try { entryName = plRow.Name; }
                                        catch { }

                                        csvData.Add(CreateDataLine(
                                            serviceName,
                                            templateName,
                                            buttonName,
                                            itemPath,
                                            entryName,
                                            conditionDesc,
                                            greaterThan,
                                            id,
                                            lessThanEqualTo
                                        ));
                                    }
                                }
                                else
                                {
                                    // Item has no product list
                                    csvData.Add(CreateDataLine(
                                        serviceName,
                                        templateName,
                                        buttonName,
                                        itemPath,
                                        "N/A",
                                        "N/A",
                                        "N/A",
                                        "N/A",
                                        "N/A"
                                    ));
                                }
                            }
                        }
                    }
                }

                ReportProgress(95, 100, $"Completed processing {processedItems} items");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating item data CSV: {ex.Message}", ex);
            }

            return csvData;
        }
    }
}
