using System;
using System.Collections.Generic;
using Autodesk.Fabrication.Content;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for items showing their assigned installation times tables.
    /// Useful for reviewing which installation table each item is configured to use.
    /// </summary>
    public class ItemInstallationTablesExportService : CsvExportService
    {
        /// <summary>
        /// Generate item installation tables CSV export.
        /// Includes service name, button name, item path, product list flag, and installation table details.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Add header
                csvData.Add(CreateHeaderLine(
                    "ServiceName",
                    "ButtonName",
                    "ItemPath",
                    "IsProductList",
                    "InstallTableName",
                    "InstallTableGroup",
                    "InstallTableType",
                    "InstallTableClass"
                ));

                int itemCount = 0;
                int itemsWithTable = 0;
                int totalItems = 0;

                // First pass: count items
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

                // Second pass: process items
                foreach (var service in FabDB.Services)
                {
                    if (IsCancelled) return csvData;

                    var serviceName = service.Name;
                    var serviceTemplate = service.ServiceTemplate;
                    if (serviceTemplate?.ServiceTabs == null) continue;

                    foreach (var tab in serviceTemplate.ServiceTabs)
                    {
                        if (tab.ServiceButtons == null) continue;

                        foreach (var button in tab.ServiceButtons)
                        {
                            if (button.ServiceButtonItems == null) continue;

                            foreach (var sbItem in button.ServiceButtonItems)
                            {
                                if (IsCancelled) return csvData;

                                try
                                {
                                    var item = ContentManager.LoadItem(sbItem.ItemPath);
                                    if (item == null) continue;

                                    itemCount++;

                                    if (itemCount % 100 == 0)
                                    {
                                        int progress = 10 + (int)((itemCount / (double)totalItems) * 80);
                                        ReportProgress(progress, 100, $"Processing items... {itemCount}/{totalItems}");
                                    }

                                    string isProductList = "No";
                                    string installTableName = "N/A";
                                    string installTableGroup = "N/A";
                                    string installTableType = "N/A";
                                    string installTableClass = "N/A";

                                    try
                                    {
                                        isProductList = item.IsProductList ? "Yes" : "No";
                                    }
                                    catch { }

                                    try
                                    {
                                        var installTable = item.InstallationTimesTable;
                                        if (installTable != null)
                                        {
                                            itemsWithTable++;
                                            installTableName = installTable.Name ?? "N/A";
                                            installTableGroup = installTable.Group ?? "N/A";
                                            installTableType = installTable.Type.ToString();
                                            installTableClass = installTable.GetType().Name;
                                        }
                                    }
                                    catch { }

                                    csvData.Add(CreateDataLine(
                                        serviceName,
                                        button.Name,
                                        item.FilePath ?? "N/A",
                                        isProductList,
                                        installTableName,
                                        installTableGroup,
                                        installTableType,
                                        installTableClass
                                    ));
                                }
                                catch { }
                            }
                        }
                    }
                }

                ReportProgress(95, 100, $"Processed {itemCount} items, {itemsWithTable} with installation tables");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating item installation tables CSV: {ex.Message}", ex);
            }

            return csvData;
        }
    }
}
