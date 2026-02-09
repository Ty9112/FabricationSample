using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for Item Statuses.
    /// Exports all item statuses from the database to CSV format.
    /// </summary>
    public class ItemStatusesExportService : CsvExportService
    {
        /// <summary>
        /// Generate CSV data for item statuses.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var lines = new List<string>();

            // Add header
            if (options.IncludeHeader)
            {
                lines.Add(CreateHeaderLine("Name", "LayerTag", "Color", "Output"));
            }

            var statuses = Database.ItemStatuses;
            if (statuses == null || statuses.Count == 0)
            {
                ReportProgress(100, 100, "No item statuses found");
                return lines;
            }

            int count = 0;
            int total = statuses.Count;

            foreach (ItemStatus status in statuses)
            {
                if (IsCancelled)
                    break;

                lines.Add(CreateDataLine(
                    status.Name ?? "",
                    status.LayerTag ?? "",
                    status.Color,
                    status.Output ? "True" : "False"
                ));

                count++;
                ReportProgress(count, total, $"Exporting status {count} of {total}");
            }

            return lines;
        }
    }
}
