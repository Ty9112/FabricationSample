using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for Specifications.
    /// Exports all specifications from the database to CSV format.
    /// </summary>
    public class SpecificationsExportService : CsvExportService
    {
        /// <summary>
        /// Generate CSV data for specifications.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var lines = new List<string>();

            // Add header
            if (options.IncludeHeader)
            {
                lines.Add(CreateHeaderLine("Name", "Group"));
            }

            var specs = Database.Specifications;
            if (specs == null || specs.Count == 0)
            {
                ReportProgress(100, 100, "No specifications found");
                return lines;
            }

            int count = 0;
            int total = specs.Count;

            foreach (Specification spec in specs)
            {
                if (IsCancelled)
                    break;

                lines.Add(CreateDataLine(
                    spec.Name ?? "",
                    spec.Group ?? ""
                ));

                count++;
                ReportProgress(count, total, $"Exporting specification {count} of {total}");
            }

            return lines;
        }
    }
}
