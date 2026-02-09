using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for Job Statuses.
    /// Exports all job statuses from the database to CSV format.
    /// </summary>
    public class JobStatusesExportService : CsvExportService
    {
        /// <summary>
        /// Generate CSV data for job statuses.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var lines = new List<string>();

            // Add header
            if (options.IncludeHeader)
            {
                lines.Add(CreateHeaderLine(
                    "Description",
                    "Active",
                    "DoCopy",
                    "CopyJobToFolder",
                    "DoSave",
                    "DoExport",
                    "ExportFile",
                    "DeActivateOnCompletion"
                ));
            }

            var statuses = Database.JobStatuses;
            if (statuses == null || statuses.Count == 0)
            {
                ReportProgress(100, 100, "No job statuses found");
                return lines;
            }

            int count = 0;
            int total = statuses.Count;

            foreach (JobStatus status in statuses)
            {
                if (IsCancelled)
                    break;

                lines.Add(CreateDataLine(
                    status.Description ?? "",
                    status.Active ? "True" : "False",
                    status.DoCopy.ToString(),
                    status.CopyJobToFolder ?? "",
                    status.DoSave ? "True" : "False",
                    status.DoExport ? "True" : "False",
                    status.ExportFile ?? "",
                    status.DeActivateOnCompletion ? "True" : "False"
                ));

                count++;
                ReportProgress(count, total, $"Exporting job status {count} of {total}");
            }

            return lines;
        }
    }
}
