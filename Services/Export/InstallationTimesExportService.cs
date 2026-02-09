using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;
// Note: InstallTableItem is defined in FabricationSample namespace (in InstallTableSelectionWindow.xaml.cs)

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for installation times tables including both simple and breakpoint tables.
    /// Creates multiple CSV files: one for simple tables and separate files for each breakpoint table.
    /// </summary>
    public class InstallationTimesExportService
    {
        /// <summary>
        /// Event raised to report progress during export.
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        /// <summary>
        /// List of selected installation tables to export. If null or empty, all tables are exported.
        /// </summary>
        public List<InstallTableItem> SelectedTables { get; set; }

        private bool _cancelled = false;

        /// <summary>
        /// Export installation times to a folder structure.
        /// </summary>
        /// <param name="outputFolder">Folder to contain all installation times exports</param>
        /// <param name="options">Export configuration options</param>
        /// <returns>Export result with file count and status</returns>
        public InstallationTimesExportResult Export(string outputFolder, ExportOptions options = null)
        {
            try
            {
                _cancelled = false;
                options = options ?? new ExportOptions();

                // Validate output folder
                if (string.IsNullOrEmpty(outputFolder))
                    return new InstallationTimesExportResult { IsSuccess = false, ErrorMessage = "Output folder cannot be empty" };

                // Create output folder
                try
                {
                    FileHelpers.EnsureDirectoryExists(outputFolder);
                }
                catch (Exception ex)
                {
                    return new InstallationTimesExportResult { IsSuccess = false, ErrorMessage = $"Failed to create directory: {ex.Message}" };
                }

                ReportProgress(5, 100, "Processing installation times tables...");

                // Generate installation times data
                string installListPath = Path.Combine(outputFolder, "InstallationProducts.csv");
                var installListCsv = new List<string> { CsvHelpers.WrapForCsv("TableName", "TableGroup", "TableType", "TableClass", "Id", "LaborRate", "Units", "Status") };
                var installBreakPointCsv = new Dictionary<string, List<string>>();

                int simpleTableCount = 0;
                int breakpointTableCount = 0;
                int otherTableCount = 0;

                var installTables = FabDB.InstallationTimesTable;
                int totalTables = installTables?.Count() ?? 0;
                int tablesProcessed = 0;

                ReportProgress(10, 100, $"Found {totalTables} installation times tables");

                if (installTables != null)
                {
                    foreach (var table in installTables)
                    {
                        if (_cancelled) return new InstallationTimesExportResult { WasCancelled = true };

                        tablesProcessed++;
                        int progress = 10 + (int)((tablesProcessed / (double)totalTables) * 75);
                        ReportProgress(progress, 100, $"Processing table {tablesProcessed}/{totalTables}");

                        var tableName = table.Name ?? "Unknown";
                        var tableGroup = table.Group ?? "N/A";
                        var tableType = table.Type.ToString();
                        var tableClass = table.GetType().Name;

                        // Skip if selective export and this table is not selected
                        if (SelectedTables != null && SelectedTables.Count > 0)
                        {
                            bool isSelected = SelectedTables.Any(t =>
                                t.TableName == tableName && t.TableGroup == (table.Group ?? string.Empty));
                            if (!isSelected)
                                continue;
                        }

                        // Try casting to specific types
                        var asSimple = table as InstallationTimesTable;
                        var asBreakpoint = table as InstallationTimesTableWithBreakpoints;

                        if (asSimple != null && asBreakpoint == null)
                        {
                            // Simple table
                            simpleTableCount++;
                            foreach (var entry in asSimple.Products)
                            {
                                var status = entry.Status == ProductEntryStatus.Active ? "Active" :
                                    entry.Status == ProductEntryStatus.PriceOnApplication ? "POA" : "Discon";

                                installListCsv.Add(CsvHelpers.WrapForCsv(
                                    tableName,
                                    tableGroup,
                                    "Simple",
                                    tableClass,
                                    entry.DatabaseId,
                                    entry.Value,
                                    entry.CostedByLength ? "per(ft)" : "(each)",
                                    status
                                ));
                            }
                        }
                        else if (asBreakpoint != null)
                        {
                            // Breakpoint table
                            breakpointTableCount++;

                            var bpKey = CsvHelpers.SanitizeFileName($"{tableGroup}_{tableName}".Replace(" ", "_"));
                            installBreakPointCsv[bpKey] = new List<string>();
                            installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("TableName", tableName));
                            installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("TableGroup", tableGroup));
                            installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("TableType", tableType));
                            installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("TableClass", tableClass));

                            try
                            {
                                installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("CostedBy", asBreakpoint.CostedBy.ToString()));
                                installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("HorizontalUnits", asBreakpoint.HorizonatalUnits.ToString()));
                                installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("HorizontalBreakPointType", asBreakpoint.HorizontalBreakPointType.ToString()));
                                installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("VerticalBreakPointType", asBreakpoint.VerticalBreakPointType.ToString()));
                                installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv("VerticalUnits", asBreakpoint.VerticalUnits.ToString()));

                                var bpTableData = asBreakpoint.Table;
                                if (bpTableData != null)
                                {
                                    var hBreakpoints = bpTableData.HorizontalBreakPoints?.OrderBy(h => h).ToList() ?? new List<double>();
                                    var vBreakpoints = bpTableData.VerticalBreakPoints?.OrderBy(v => v).ToList() ?? new List<double>();

                                    // Add header row (V\H, then horizontal breakpoints)
                                    var headerRow = new List<string> { "V\\H" };
                                    headerRow.AddRange(hBreakpoints.Select(h => h.ToString()));
                                    installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv((object[])headerRow.ToArray()));

                                    // Add data rows
                                    foreach (var vBp in vBreakpoints)
                                    {
                                        var rowValues = new List<string> { vBp.ToString() };
                                        int vIndex = vBreakpoints.IndexOf(vBp);

                                        foreach (var hBp in hBreakpoints)
                                        {
                                            int hIndex = hBreakpoints.IndexOf(hBp);
                                            try
                                            {
                                                var cellResult = bpTableData.GetValue(hIndex, vIndex);
                                                rowValues.Add(cellResult.ReturnObject?.ToString() ?? "N/A");
                                            }
                                            catch
                                            {
                                                rowValues.Add("N/A");
                                            }
                                        }
                                        installBreakPointCsv[bpKey].Add(CsvHelpers.WrapForCsv((object[])rowValues.ToArray()));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                installBreakPointCsv[bpKey].Add($"Error: {ex.Message}");
                            }
                        }
                        else
                        {
                            otherTableCount++;
                        }
                    }
                }

                if (_cancelled) return new InstallationTimesExportResult { WasCancelled = true };

                // Write files
                ReportProgress(90, 100, "Writing files...");

                File.WriteAllLines(installListPath, installListCsv);
                int filesWritten = 1;

                foreach (var kvp in installBreakPointCsv)
                {
                    var bpPath = Path.Combine(outputFolder, $"InstallBreakPoints_{kvp.Key}.csv");
                    File.WriteAllLines(bpPath, kvp.Value);
                    filesWritten++;
                }

                ReportProgress(100, 100, "Export complete");

                return new InstallationTimesExportResult
                {
                    IsSuccess = true,
                    FolderPath = outputFolder,
                    FileCount = filesWritten,
                    SimpleTableCount = simpleTableCount,
                    BreakpointTableCount = breakpointTableCount,
                    OtherTableCount = otherTableCount,
                    ProductEntryCount = installListCsv.Count - 1
                };
            }
            catch (Exception ex)
            {
                return new InstallationTimesExportResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Cancel ongoing export operation.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        /// <summary>
        /// Report progress to listeners.
        /// </summary>
        private void ReportProgress(int current, int total, string message)
        {
            if (_cancelled) return;

            int percentage = total > 0 ? (int)((current / (double)total) * 100) : 0;

            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message,
                Percentage = percentage
            });
        }
    }

    /// <summary>
    /// Result of installation times export operation.
    /// </summary>
    public class InstallationTimesExportResult
    {
        public bool IsSuccess { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }
        public string FolderPath { get; set; }
        public int FileCount { get; set; }
        public int SimpleTableCount { get; set; }
        public int BreakpointTableCount { get; set; }
        public int OtherTableCount { get; set; }
        public int ProductEntryCount { get; set; }
    }
}
