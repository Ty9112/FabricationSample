using System;
using System.Collections.Generic;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for database sections.
    /// Exports all sections with their properties to a CSV file.
    /// </summary>
    public class SectionsExportService : CsvExportService
    {
        /// <summary>
        /// Generate CSV content for sections export.
        /// </summary>
        protected override List<string> GenerateCsvData(ExportOptions options)
        {
            var csvLines = new List<string>();

            ReportProgress(0, 100, "Exporting sections...");

            // Header row
            var header = new List<string>
            {
                "Description",
                "Group",
                "Index",
                "DifficultyFactor",
                "RetroFitted",
                "RetroFittedPercentage",
                "LayerTag",
                "FloorLevel",
                "SlabLevel",
                "ColorR",
                "ColorG",
                "ColorB"
            };
            csvLines.Add(CsvHelpers.WrapForCsv((object[])header.ToArray()));

            // Data rows
            int count = 0;
            int total = Database.Sections.Count;

            foreach (var section in Database.Sections)
            {
                if (IsCancelled) break;

                var row = new List<string>
                {
                    section.Description ?? "",
                    section.Group ?? "",
                    section.Index.ToString(),
                    section.DifficultyFactor.ToString(),
                    section.RetroFitted ? "True" : "False",
                    section.RetroFittedPercentage.ToString(),
                    section.LayerTag ?? "",
                    section.FloorLevel.ToString(),
                    section.SlabLevel.ToString(),
                    section.Color.R.ToString(),
                    section.Color.G.ToString(),
                    section.Color.B.ToString()
                };

                csvLines.Add(CsvHelpers.WrapForCsv((object[])row.ToArray()));
                count++;

                int progress = (int)((count / (double)total) * 100);
                ReportProgress(progress, 100, $"Exported {count} of {total} sections...");
            }

            return csvLines;
        }
    }
}
