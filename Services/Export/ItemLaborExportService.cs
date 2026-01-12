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
    /// Export service for items with calculated labor values from breakpoint tables.
    /// Exports product list items showing their dimensions, installation table assignments, and calculated labor.
    /// </summary>
    public class ItemLaborExportService : CsvExportService
    {
        /// <summary>
        /// Generate item labor CSV export.
        /// Includes service, button, item path, database ID, dimensions, installation table, and calculated labor.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Phase 1: Build breakpoint table lookup
                ReportProgress(5, 100, "Phase 1: Building breakpoint table lookup...");

                var breakpointTables1D = new Dictionary<string, Dictionary<double, double>>();
                var breakpointTables2D = new Dictionary<string, Tuple<List<double>, List<double>, BreakPointTable>>();
                var breakpointTableInfo = new Dictionary<string, string>();

                int tables1D = 0, tables2D = 0;

                var installTables = FabDB.InstallationTimesTable;
                if (installTables != null)
                {
                    foreach (var table in installTables)
                    {
                        if (IsCancelled) return csvData;

                        if (table is InstallationTimesTableWithBreakpoints bpTable)
                        {
                            var tableName = bpTable.Name ?? "Unknown";
                            var tableGroup = bpTable.Group ?? "N/A";
                            var fullName = tableGroup + " - " + tableName;
                            var vertBpType = bpTable.VerticalBreakPointType.ToString();

                            breakpointTableInfo[fullName] = vertBpType;
                            breakpointTableInfo[tableName] = vertBpType;

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
                                tables1D++;
                            }
                            else
                            {
                                // 2D table
                                var tableInfo = Tuple.Create(vBreakpoints, hBreakpoints, bpTableData);
                                breakpointTables2D[fullName] = tableInfo;
                                breakpointTables2D[tableName] = tableInfo;
                                tables2D++;
                            }
                        }
                    }
                }

                ReportProgress(20, 100, $"Loaded {tables1D} 1D tables, {tables2D} 2D tables");

                // Phase 2: Process items with product lists
                ReportProgress(25, 100, "Phase 2: Processing items with product lists...");

                csvData.Add(CreateHeaderLine(
                    "ServiceName",
                    "ButtonName",
                    "ItemPath",
                    "DatabaseId",
                    "Dim1Value",
                    "Dim2Value",
                    "InstallTableName",
                    "TableType",
                    "LaborValue",
                    "LookupMethod"
                ));

                int itemCount = 0;
                int laborFoundCount = 0;

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

                                    // Get installation table for this item
                                    var installTable = item.InstallationTimesTable;
                                    string installTableName = "N/A";
                                    string installTableGroup = "N/A";
                                    string installTableFullName = "N/A";
                                    string verticalBpType = "N/A";

                                    if (installTable != null)
                                    {
                                        installTableName = installTable.Name ?? "N/A";
                                        installTableGroup = installTable.Group ?? "N/A";
                                        installTableFullName = installTableGroup + " - " + installTableName;

                                        if (breakpointTableInfo.TryGetValue(installTableFullName, out string bpType))
                                            verticalBpType = bpType;
                                    }

                                    // Process product list rows
                                    if (item.IsProductList && item.ProductList?.Rows != null)
                                    {
                                        foreach (var row in item.ProductList.Rows)
                                        {
                                            itemCount++;
                                            string dbId = row.DatabaseId ?? "N/A";
                                            if (string.IsNullOrEmpty(dbId) || dbId == "N/A") continue;

                                            // Get dimensions
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

                                            // Look up labor value
                                            string laborValue = "N/A";
                                            string lookupMethod = "N/A";
                                            string tableType = "N/A";

                                            if (installTable == null)
                                            {
                                                lookupMethod = "No install table";
                                            }
                                            else if (breakpointTables1D.TryGetValue(installTableFullName, out var lookup1D))
                                            {
                                                tableType = "1D";
                                                if (dim1Value > 0)
                                                {
                                                    var matchingBp = lookup1D.Keys.Where(bp => bp <= dim1Value).OrderByDescending(bp => bp).FirstOrDefault();
                                                    if (lookup1D.TryGetValue(matchingBp, out double labor))
                                                    {
                                                        laborValue = labor.ToString();
                                                        lookupMethod = $"V={matchingBp}";
                                                        laborFoundCount++;
                                                    }
                                                    else
                                                    {
                                                        lookupMethod = $"Dim1={dim1Value} - no BP match";
                                                    }
                                                }
                                                else
                                                {
                                                    lookupMethod = "Dim1 is 0";
                                                }
                                            }
                                            else if (breakpointTables2D.TryGetValue(installTableFullName, out var lookup2D))
                                            {
                                                tableType = "2D";
                                                var vBreakpoints = lookup2D.Item1;
                                                var hBreakpoints = lookup2D.Item2;
                                                var bpTableData = lookup2D.Item3;

                                                if (dim1Value > 0 && dim2Value > 0)
                                                {
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
                                                            {
                                                                laborValue = cellResult.ReturnObject.ToString();
                                                                lookupMethod = $"V={matchingV},H={matchingH}";
                                                                laborFoundCount++;
                                                            }
                                                            else
                                                            {
                                                                lookupMethod = $"V={matchingV},H={matchingH} - null";
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            lookupMethod = $"GetValue error: {ex.Message}";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        lookupMethod = $"BP index not found (V:{vIdx},H:{hIdx})";
                                                    }
                                                }
                                                else if (dim1Value > 0)
                                                {
                                                    lookupMethod = "2D table but Dim2 is 0";
                                                }
                                                else
                                                {
                                                    lookupMethod = "Dim1 is 0";
                                                }
                                            }
                                            else
                                            {
                                                lookupMethod = "Table not in BP lookup";
                                            }

                                            csvData.Add(CreateDataLine(
                                                serviceName,
                                                button.Name,
                                                item.FilePath ?? "N/A",
                                                dbId,
                                                dim1Value.ToString(),
                                                dim2Value.ToString(),
                                                installTableFullName,
                                                tableType,
                                                laborValue,
                                                lookupMethod
                                            ));

                                            if (itemCount % 500 == 0)
                                            {
                                                int progress = 25 + (int)((itemCount / 5000.0) * 65);
                                                ReportProgress(progress, 100, $"Processed {itemCount} product entries...");
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                ReportProgress(95, 100, $"Total: {itemCount} entries, {laborFoundCount} with labor values");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating item labor CSV: {ex.Message}", ex);
            }

            return csvData;
        }
    }
}
