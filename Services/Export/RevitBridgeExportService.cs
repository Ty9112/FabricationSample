using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for job items in a flat CSV format suitable for Dynamo/Power BI consumption.
    /// Iterates Job.Items and exports all properties keyed by UniqueId.
    /// </summary>
    public class RevitBridgeExportService : CsvExportService
    {
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                var jobItems = Job.Items;
                if (jobItems == null || jobItems.Count == 0)
                {
                    ReportProgress(100, 100, "No job items found.");
                    return csvData;
                }

                // Determine max dimensions and custom data count from first item
                int maxDims = 0;
                int maxCustomData = 0;
                try
                {
                    foreach (Item item in jobItems)
                    {
                        try
                        {
                            if (item.Dimensions != null && item.Dimensions.Count > maxDims)
                                maxDims = item.Dimensions.Count;
                            if (item.CustomData != null && item.CustomData.Count > maxCustomData)
                                maxCustomData = item.CustomData.Count;
                        }
                        catch { }
                    }
                }
                catch { }

                // Build header
                var headerParts = new List<string>
                {
                    "UniqueId", "UniqueIdBase64", "Name", "SourceDescription", "Number",
                    "CID", "PatternNumber", "Status", "Section", "Service",
                    "ServiceTemplate", "Specification", "InsulationSpecification",
                    "DatabaseId", "Material", "Gauge", "Weight", "SKey",
                    "Order", "Zone", "Notes", "IsHiddenInViews"
                };

                for (int d = 1; d <= maxDims; d++)
                    headerParts.Add($"Dim{d}");
                for (int c = 1; c <= maxCustomData; c++)
                    headerParts.Add($"CustomData{c}");

                csvData.Add(CreateHeaderLine(headerParts.ToArray()));

                int total = jobItems.Count;
                ReportProgress(5, 100, $"Processing {total} job items...");

                for (int i = 0; i < total; i++)
                {
                    if (IsCancelled) return csvData;

                    Item item = jobItems[i];

                    if (i % 50 == 0)
                    {
                        int progress = 5 + (int)((i / (double)total) * 90);
                        ReportProgress(progress, 100, $"Processing items... {i}/{total}");
                    }

                    var values = new List<object>();

                    values.Add(SafeGet(() => item.UniqueId));
                    values.Add(SafeGet(() => item.UniqueIdBase64));
                    values.Add(SafeGet(() => item.Name));
                    values.Add(SafeGet(() => item.SourceDescription));
                    values.Add(SafeGet(() => item.Number));
                    values.Add(SafeGet(() => item.CID));
                    values.Add(SafeGet(() => item.PatternNumber));
                    values.Add(SafeGet(() => item.Status?.Name));
                    values.Add(SafeGet(() => item.Section?.Description));
                    values.Add(SafeGet(() => item.Service?.Name));
                    values.Add(SafeGet(() => item.Service?.ServiceTemplate?.Name));
                    values.Add(SafeGet(() => item.Specification?.Name));
                    values.Add(SafeGet(() => item.InsulationSpecification));
                    values.Add(SafeGet(() => item.DatabaseId));
                    values.Add(SafeGet(() => item.Material?.Name));
                    values.Add(SafeGet(() => item.Gauge?.Thickness));
                    values.Add(SafeGet(() => item.Weight));
                    values.Add(SafeGet(() => item.SKey));
                    values.Add(SafeGet(() => item.Order));
                    values.Add(SafeGet(() => item.Zone));
                    values.Add(SafeGet(() => item.Notes));
                    values.Add(SafeGet(() => item.IsHiddenInViews));

                    // Dimensions
                    for (int d = 0; d < maxDims; d++)
                    {
                        try
                        {
                            if (item.Dimensions != null && d < item.Dimensions.Count)
                                values.Add(item.Dimensions[d].Value);
                            else
                                values.Add("");
                        }
                        catch { values.Add(""); }
                    }

                    // Custom Data
                    for (int c = 0; c < maxCustomData; c++)
                    {
                        try
                        {
                            if (item.CustomData != null && c < item.CustomData.Count)
                                values.Add(GetCustomDataValue(item.CustomData[c]));
                            else
                                values.Add("");
                        }
                        catch { values.Add(""); }
                    }

                    csvData.Add(CreateDataLine(values.ToArray()));
                }

                ReportProgress(98, 100, $"Completed processing {total} items");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating Revit bridge CSV: {ex.Message}", ex);
            }

            return csvData;
        }

        private static string GetCustomDataValue(CustomItemData cd)
        {
            try
            {
                if (cd is CustomDataStringValue sv) return sv.Value ?? "";
                if (cd is CustomDataIntegerValue iv) return iv.Value.ToString();
                if (cd is CustomDataDoubleValue dv) return dv.Value.ToString();
                return cd.Data?.Description ?? "";
            }
            catch { return ""; }
        }

        private static string SafeGet(Func<object> getter)
        {
            try
            {
                var val = getter();
                return val?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
