using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for comprehensive product information including prices and labor.
    /// Implements the most comprehensive export from DiscordCADmep's GetProductInfo.
    /// </summary>
    public class ProductInfoExportService : CsvExportService
    {
        /// <summary>
        /// Generate comprehensive product info CSV data.
        /// Includes product definitions, supplier IDs, price lists, installation times, and breakpoint labor.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Phase 1: Scan items for product list entries
                ReportProgress(5, 100, "Phase 1: Scanning items for product list entries...");
                var productListedNames = ScanProductListedNames();

                if (IsCancelled) return csvData;

                // Phase 2: Process product definitions
                ReportProgress(20, 100, "Phase 2: Processing product definitions...");
                var header = new List<string> { "Id", "IsProductListed", "ProductGroup", "Manufacturer",
                    "ProductName", "Description", "Size", "Material", "Specification", "InstallType",
                    "Source", "Range", "Finish", "(skip)", "Id" };
                var prodDefs = new Dictionary<string, string>();

                foreach (var productDef in ProductDatabase.ProductDefinitions)
                {
                    if (IsCancelled) return csvData;

                    var id = productDef.Id;
                    if (string.IsNullOrEmpty(id) || id == "N/A") continue;

                    var isProductListed = productListedNames.Contains(id) ? "Yes" : "No";
                    string productGroup = "N/A";
                    try { productGroup = productDef.Group != null ? productDef.Group.Name : "N/A"; } catch { }

                    var externalIds = new List<string>();
                    foreach (var supplierIdDef in productDef.SupplierIds)
                    {
                        string supplierName = supplierIdDef.ProductSupplier?.Name ?? "N/A";
                        string externalId = supplierIdDef.Id ?? "N/A";
                        externalIds.Add(externalId);
                        if (!header.Contains(supplierName)) header.Add(supplierName);
                    }

                    var part1 = CsvHelpers.WrapForCsv(id, isProductListed, productGroup, productDef.Manufacturer,
                        productDef.ProductName, productDef.Description, productDef.Size, productDef.Material,
                        productDef.Specification, productDef.InstallType, productDef.Source, productDef.Range,
                        productDef.Finish, "N/A", id);
                    var part2 = CsvHelpers.WrapForCsv((object[])externalIds.ToArray());
                    prodDefs[id] = $"{part1},{part2},";
                }
                var prodDefColumns = header.Count;

                if (IsCancelled) return csvData;

                // Add price and installation columns to header
                header.AddRange(new List<string> { "(skip)", "(ignore)SupplierGroup", "(ignore)PriceListName",
                    "Id", "Cost", "Discount", "Units", "Date DD/MM/YYYY", "Status" });
                header.AddRange(new List<string> { "(skip)", "(ignore)InstallTableName", "InstallId",
                    "LaborRate", "LaborUnits", "LaborStatus" });

                // Phase 3: Collect price lists
                ReportProgress(40, 100, "Phase 3: Collecting price lists...");
                var priceLists = CollectPriceLists();

                if (IsCancelled) return csvData;

                // Phase 4: Collect installation times
                ReportProgress(60, 100, "Phase 4: Collecting installation times...");
                var installationTimes = CollectInstallationTimes();

                if (IsCancelled) return csvData;

                // Phase 4.5: Build breakpoint labor lookup
                ReportProgress(70, 100, "Phase 4.5: Building breakpoint labor lookup...");
                var breakpointLabor = BuildBreakpointLaborLookup();

                if (IsCancelled) return csvData;

                // Add breakpoint labor columns to header
                header.AddRange(new List<string> { "BpInstallTableName", "BpLaborValue" });

                // Phase 5: Build CSV output
                ReportProgress(85, 100, "Phase 5: Building CSV output...");
                csvData.Add(CsvHelpers.WrapForCsv((object[])header.ToArray()));

                var skipProdDefColumns = string.Join(",", Enumerable.Repeat("N/A", prodDefColumns).Select(s => s.WrapForCsv())) + ",";
                var skipPriceListColumns = string.Join(",", Enumerable.Repeat("N/A", 9).Select(s => s.WrapForCsv()));
                var skipInstallColumns = string.Join(",", Enumerable.Repeat("N/A", 6).Select(s => s.WrapForCsv()));
                var skipBpLaborColumns = "N/A".WrapForCsv() + "," + "N/A".WrapForCsv();

                var allIds = new HashSet<string>();
                foreach (var key in prodDefs.Keys) allIds.Add(key);
                foreach (var key in priceLists.Keys) allIds.Add(key);
                foreach (var key in installationTimes.Keys) allIds.Add(key);
                foreach (var key in breakpointLabor.Keys) allIds.Add(key);

                foreach (var id in allIds)
                {
                    if (IsCancelled) return csvData;

                    if (string.IsNullOrEmpty(id) || id == "N/A") continue;

                    bool hasProdDef = prodDefs.TryGetValue(id, out var prodDefLine);
                    bool hasPriceList = priceLists.TryGetValue(id, out var priceListEntries);
                    bool hasInstallTimes = installationTimes.TryGetValue(id, out var installEntries);
                    bool hasBpLabor = breakpointLabor.TryGetValue(id, out var bpLaborEntry);
                    int maxRows = Math.Max(hasPriceList ? priceListEntries.Count : 1, hasInstallTimes ? installEntries.Count : 1);

                    var idOnlyProdPart = id.WrapForCsv() + "," + string.Join(",", Enumerable.Repeat("N/A", prodDefColumns - 1).Select(s => s.WrapForCsv())) + ",";
                    string bpLaborPart = hasBpLabor ? CsvHelpers.WrapForCsv(bpLaborEntry.Item1, bpLaborEntry.Item2) : skipBpLaborColumns;

                    for (int row = 0; row < maxRows; row++)
                    {
                        string prodPart;
                        if (row == 0 && hasProdDef)
                            prodPart = prodDefLine;
                        else if (row == 0)
                            prodPart = idOnlyProdPart;
                        else
                            prodPart = skipProdDefColumns;

                        string pricePart = hasPriceList && row < priceListEntries.Count ? priceListEntries[row] : skipPriceListColumns;
                        string installPart = hasInstallTimes && row < installEntries.Count ? installEntries[row] : skipInstallColumns;
                        string rowBpLabor = row == 0 ? bpLaborPart : skipBpLaborColumns;
                        csvData.Add($"{prodPart}{pricePart},{installPart},{rowBpLabor}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating product info CSV: {ex.Message}", ex);
            }

            return csvData;
        }

        /// <summary>
        /// Scan all items to find which database IDs are in product lists.
        /// </summary>
        private HashSet<string> ScanProductListedNames()
        {
            var productListedNames = new HashSet<string>();

            foreach (var service in FabDB.Services)
            {
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
                            try
                            {
                                var item = ContentManager.LoadItem(sbItem.ItemPath);
                                if (item != null && item.IsProductList && item.ProductList?.Rows != null)
                                {
                                    foreach (var row in item.ProductList.Rows)
                                    {
                                        try { if (!string.IsNullOrEmpty(row.Name)) productListedNames.Add(row.Name); } catch { }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            return productListedNames;
        }

        /// <summary>
        /// Collect all price lists from supplier groups.
        /// </summary>
        private Dictionary<string, List<string>> CollectPriceLists()
        {
            var priceLists = new Dictionary<string, List<string>>();

            foreach (var supplierGroup in FabDB.SupplierGroups)
            {
                var groupName = supplierGroup.Name;
                foreach (var list in supplierGroup.PriceLists)
                {
                    if (list is PriceList priceList)
                    {
                        var listName = priceList.Name;
                        foreach (var entry in priceList.Products)
                        {
                            var id = entry.DatabaseId;
                            if (string.IsNullOrEmpty(id) || id == "N/A") continue;

                            var status = entry.Status == ProductEntryStatus.Active ? "Active" :
                                entry.Status == ProductEntryStatus.PriceOnApplication ? "POA" : "Discon";
                            var part3 = CsvHelpers.WrapForCsv("N/A", groupName, listName, id, entry.Value,
                                entry.DiscountCode, entry.CostedByLength ? "per(ft)" : "(each)",
                                entry.Date.HasValue ? entry.Date.Value.ToString("dd/MM/yyyy") : "None", status);

                            if (!priceLists.ContainsKey(id))
                                priceLists.Add(id, new List<string>());
                            priceLists[id].Add(part3);
                        }
                    }
                }
            }

            return priceLists;
        }

        /// <summary>
        /// Collect installation times from simple tables.
        /// </summary>
        private Dictionary<string, List<string>> CollectInstallationTimes()
        {
            var installationTimes = new Dictionary<string, List<string>>();

            try
            {
                var installTables = FabDB.InstallationTimesTable;
                if (installTables != null)
                {
                    foreach (var table in installTables)
                    {
                        var tableName = table.Name ?? "Unknown";
                        var tableGroup = table.Group ?? "N/A";

                        if (table is InstallationTimesTable simpleTable)
                        {
                            foreach (var entry in simpleTable.Products)
                            {
                                var id = entry.DatabaseId;
                                if (string.IsNullOrEmpty(id) || id == "N/A") continue;

                                var status = entry.Status == ProductEntryStatus.Active ? "Active" :
                                    entry.Status == ProductEntryStatus.PriceOnApplication ? "POA" : "Discon";
                                var laborPart = CsvHelpers.WrapForCsv("N/A", $"{tableGroup} - {tableName}", id,
                                    entry.Value, entry.CostedByLength ? "per(ft)" : "(each)", status);

                                if (!installationTimes.ContainsKey(id))
                                    installationTimes.Add(id, new List<string>());
                                installationTimes[id].Add(laborPart);
                            }
                        }
                    }
                }
            }
            catch { }

            return installationTimes;
        }

        /// <summary>
        /// Build breakpoint labor lookup by scanning items and their installation tables.
        /// </summary>
        private Dictionary<string, Tuple<string, string>> BuildBreakpointLaborLookup()
        {
            var breakpointLabor = new Dictionary<string, Tuple<string, string>>();

            // Build 1D and 2D breakpoint table lookups
            var breakpointTables1D = new Dictionary<string, Dictionary<double, double>>();
            var breakpointTables2D = new Dictionary<string, Tuple<List<double>, List<double>, BreakPointTable>>();

            try
            {
                var installTables = FabDB.InstallationTimesTable;
                if (installTables != null)
                {
                    foreach (var table in installTables)
                    {
                        if (table is InstallationTimesTableWithBreakpoints bpTable)
                        {
                            var tableName = bpTable.Name ?? "Unknown";
                            var tableGroup = bpTable.Group ?? "N/A";
                            var fullName = tableGroup + " - " + tableName;

                            var bpTableData = bpTable.Table;
                            if (bpTableData == null) continue;

                            var vBreakpoints = bpTableData.VerticalBreakPoints != null
                                ? bpTableData.VerticalBreakPoints.OrderBy(v => v).ToList()
                                : new List<double>();
                            var hBreakpoints = bpTableData.HorizontalBreakPoints != null
                                ? bpTableData.HorizontalBreakPoints.OrderBy(h => h).ToList()
                                : new List<double>();

                            if (bpTable.HorizontalBreakPointType == TimesTableBreakPointType.None)
                            {
                                // 1D table
                                var lookup = new Dictionary<double, double>();
                                for (int vIdx = 0; vIdx < vBreakpoints.Count; vIdx++)
                                {
                                    double vBp = vBreakpoints[vIdx];
                                    try
                                    {
                                        var cellResult = bpTableData.GetValue(0, vIdx);
                                        if (cellResult.ReturnObject != null && double.TryParse(cellResult.ReturnObject.ToString(), out double val))
                                            lookup[vBp] = val;
                                    }
                                    catch { }
                                }
                                breakpointTables1D[fullName] = lookup;
                                breakpointTables1D[tableName] = lookup;
                            }
                            else
                            {
                                // 2D table
                                var tableInfo = Tuple.Create(vBreakpoints, hBreakpoints, bpTableData);
                                breakpointTables2D[fullName] = tableInfo;
                                breakpointTables2D[tableName] = tableInfo;
                            }
                        }
                    }
                }
            }
            catch { }

            // Now iterate through items to find breakpoint labor by DatabaseId
            foreach (var service in FabDB.Services)
            {
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
                            try
                            {
                                var item = ContentManager.LoadItem(sbItem.ItemPath);
                                if (item == null) continue;

                                var installTableBase = item.InstallationTimesTable;
                                var installTable = installTableBase as InstallationTimesTableWithBreakpoints;
                                if (installTable == null) continue;

                                var installTableName = installTable.Name ?? "Unknown";
                                var installTableGroup = installTable.Group ?? "N/A";
                                var installTableFullName = installTableGroup + " - " + installTableName;

                                if (item.IsProductList && item.ProductList?.Rows != null)
                                {
                                    foreach (var row in item.ProductList.Rows)
                                    {
                                        string dbId = row.DatabaseId;
                                        if (string.IsNullOrEmpty(dbId) || dbId == "N/A") continue;
                                        if (breakpointLabor.ContainsKey(dbId)) continue;

                                        double dim1Value = 0;
                                        double dim2Value = 0;
                                        try
                                        {
                                            var dims = row.Dimensions?.ToList();
                                            if (dims != null && dims.Count > 0)
                                            {
                                                dim1Value = dims[0].Value;
                                                for (int i = 1; i < dims.Count; i++)
                                                {
                                                    if (dims[i].Value < dim1Value)
                                                    {
                                                        dim2Value = dims[i].Value;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        catch { }

                                        string laborValue = "N/A";

                                        if (breakpointTables1D.TryGetValue(installTableFullName, out var lookup1D) && dim1Value > 0)
                                        {
                                            var matchingBp = lookup1D.Keys.Where(bp => bp <= dim1Value).OrderByDescending(bp => bp).FirstOrDefault();
                                            if (lookup1D.TryGetValue(matchingBp, out double labor))
                                                laborValue = labor.ToString();
                                        }
                                        else if (breakpointTables2D.TryGetValue(installTableFullName, out var lookup2D) && dim1Value > 0 && dim2Value > 0)
                                        {
                                            var vBreakpoints = lookup2D.Item1;
                                            var hBreakpoints = lookup2D.Item2;
                                            var bpTableData = lookup2D.Item3;

                                            var matchingV = vBreakpoints.Where(bp => bp <= dim1Value).OrderByDescending(bp => bp).FirstOrDefault();
                                            int vIdx = vBreakpoints.IndexOf(matchingV);
                                            var matchingH = hBreakpoints.Where(bp => bp <= dim2Value).OrderByDescending(bp => bp).FirstOrDefault();
                                            int hIdx = hBreakpoints.IndexOf(matchingH);

                                            if (vIdx >= 0 && hIdx >= 0)
                                            {
                                                try
                                                {
                                                    var cellResult = bpTableData.GetValue(hIdx, vIdx);
                                                    if (cellResult.ReturnObject != null)
                                                        laborValue = cellResult.ReturnObject.ToString();
                                                }
                                                catch { }
                                            }
                                        }

                                        breakpointLabor[dbId] = Tuple.Create(installTableFullName, laborValue);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            return breakpointLabor;
        }
    }
}
