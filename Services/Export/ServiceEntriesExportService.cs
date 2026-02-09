using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for service entries (layer configuration, service types).
    /// Exports service entries with their layer tags, colors, blocks, line weights, and insulation settings.
    /// </summary>
    public class ServiceEntriesExportService : CsvExportService
    {
        /// <summary>
        /// Services to export entries from (null = all services)
        /// </summary>
        public List<string> SelectedServiceNames { get; set; }

        /// <summary>
        /// Generate service entries CSV export.
        /// Exports format: Service Name, Service Type, Layer Tag 1, Layer Tag 2, Layer Color,
        /// Level Block, Size Block, Includes Insulation, Line Weight
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvData = new List<string>();

            try
            {
                // Add header
                csvData.Add(CsvHelpers.WrapForCsv(
                    "Service Name",
                    "Service Type",
                    "Layer Tag 1",
                    "Layer Tag 2",
                    "Layer Color",
                    "Level Block",
                    "Size Block",
                    "Includes Insulation",
                    "Line Weight"
                ));

                int totalServices = FabDB.Services.Count();
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

                    if (IsCancelled) return csvData;

                    processedServices++;
                    ReportProgress(10 + (int)((processedServices / (double)totalServices) * 80), 100,
                        $"Processing service {processedServices}/{totalServices}: {service.Name}");

                    string serviceName = service.Name;

                    // Check if service has entries
                    if (service.ServiceEntries == null || !service.ServiceEntries.Any())
                    {
                        // Add a row indicating no entries
                        csvData.Add(CsvHelpers.WrapForCsv(
                            serviceName,
                            "No Entries",
                            "",
                            "",
                            "",
                            "",
                            "",
                            "",
                            ""
                        ));
                        continue;
                    }

                    // Process each service entry
                    foreach (var entry in service.ServiceEntries)
                    {
                        if (IsCancelled) return csvData;

                        string serviceType = entry.ServiceType?.Description ?? "Unknown";
                        string layerTag1 = entry.LayerTag1 ?? "";
                        string layerTag2 = entry.LayerTag2 ?? "";
                        string layerColor = entry.LayerColor.ToString();
                        string levelBlock = entry.LevelBlock ?? "";
                        string sizeBlock = entry.SizeBlock ?? "";
                        string includesInsulation = entry.IncludesInsulation ? "Yes" : "No";
                        string lineWeight = entry.LineWeight?.LineWeightValue.ToString() ?? "";

                        csvData.Add(CsvHelpers.WrapForCsv(
                            serviceName,
                            serviceType,
                            layerTag1,
                            layerTag2,
                            layerColor,
                            levelBlock,
                            sizeBlock,
                            includesInsulation,
                            lineWeight
                        ));
                    }
                }

                ReportProgress(95, 100, $"Completed processing {processedServices} services");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating service entries CSV: {ex.Message}", ex);
            }

            return csvData;
        }
    }
}
