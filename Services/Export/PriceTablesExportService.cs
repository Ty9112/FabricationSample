using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;
// Note: PriceTableItem is defined in FabricationSample namespace (in PriceTableSelectionWindow.xaml.cs)

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Export service for price tables including both simple and breakpoint price lists.
    /// Creates multiple CSV files: one for simple price lists and separate files for each breakpoint table.
    /// </summary>
    public class PriceTablesExportService
    {
        /// <summary>
        /// Event raised to report progress during export.
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        /// <summary>
        /// List of selected price tables to export. If null or empty, all tables are exported.
        /// </summary>
        public List<PriceTableItem> SelectedTables { get; set; }

        private bool _cancelled = false;

        /// <summary>
        /// Export price tables to a folder structure.
        /// </summary>
        /// <param name="outputFolder">Folder to contain all price table exports</param>
        /// <param name="options">Export configuration options</param>
        /// <returns>Export result with file count and status</returns>
        public PriceTablesExportResult Export(string outputFolder, ExportOptions options = null)
        {
            try
            {
                _cancelled = false;
                options = options ?? new ExportOptions();

                // Validate output folder
                if (string.IsNullOrEmpty(outputFolder))
                    return new PriceTablesExportResult { IsSuccess = false, ErrorMessage = "Output folder cannot be empty" };

                // Create output folder
                try
                {
                    FileHelpers.EnsureDirectoryExists(outputFolder);
                }
                catch (Exception ex)
                {
                    return new PriceTablesExportResult { IsSuccess = false, ErrorMessage = $"Failed to create directory: {ex.Message}" };
                }

                ReportProgress(5, 100, "Processing price lists...");

                // Generate price list data
                string priceListPath = Path.Combine(outputFolder, "PriceLists.csv");
                var priceListCsv = new List<string> { CsvHelpers.WrapForCsv("SupplierGroup", "PriceListName", "Id", "Cost", "Discount", "Units", "Date", "Status") };
                var priceBreakPointCsv = new Dictionary<string, List<string>>();

                int groupsProcessed = 0;
                int totalGroups = FabDB.SupplierGroups?.Count() ?? 0;

                foreach (var group in FabDB.SupplierGroups)
                {
                    if (_cancelled) return new PriceTablesExportResult { WasCancelled = true };

                    groupsProcessed++;
                    int progress = 5 + (int)((groupsProcessed / (double)totalGroups) * 80);
                    ReportProgress(progress, 100, $"Processing supplier group {groupsProcessed}/{totalGroups}: {group.Name}");

                    var groupName = group.Name;
                    foreach (var list in group.PriceLists)
                    {
                        var listName = list.Name;

                        // Skip if selective export and this table is not selected
                        if (SelectedTables != null && SelectedTables.Count > 0)
                        {
                            bool isSelected = SelectedTables.Any(t =>
                                t.SupplierGroupName == groupName && t.PriceListName == listName);
                            if (!isSelected)
                                continue;
                        }

                        // Handle simple price lists
                        if (list is PriceList priceList)
                        {
                            foreach (var entry in priceList.Products)
                            {
                                var id = entry.DatabaseId;
                                var costedByLength = entry.CostedByLength;
                                var date = entry.Date.HasValue ? entry.Date.Value.ToString("dd/MM/yyyy") : "None";
                                var discountCode = entry.DiscountCode;
                                var status = entry.Status;
                                var value = entry.Value;

                                priceListCsv.Add(CsvHelpers.WrapForCsv(
                                    groupName,
                                    listName,
                                    id,
                                    value,
                                    discountCode,
                                    costedByLength ? "per(ft)" : "(each)",
                                    date,
                                    status.ToString()
                                ));
                            }
                        }
                        // Handle breakpoint price lists
                        else if (list is PriceListWithBreakPoints bpList)
                        {
                            var table = bpList.DefaultTable;
                            if (table == null) continue;

                            priceBreakPointCsv.Add(listName, new List<string>());
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("SupplierGroup", groupName));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("PriceListName", listName));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("CostedBy", bpList.CostedBy.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("HorizontalUnits", bpList.HorizonatalUnits.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("HorizontalBreakPointType", bpList.HorizontalBreakPointType.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("HorizontalCompareBy", bpList.HorizontalCompareBy.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("VerticalBreakPointType", bpList.VerticalBreakPointType.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("VerticalCompareBy", bpList.VerticalCompareBy.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv("VerticalUnits", bpList.VerticalUnits.ToString()));

                            // Build breakpoint table matrix
                            var vBreakpoints = table.VerticalBreakPoints?.OrderBy(v => v).ToList() ?? new List<double>();
                            var hBreakpoints = table.HorizontalBreakPoints?.OrderBy(h => h).ToList() ?? new List<double>();

                            // Add header row (V\H, then horizontal breakpoints)
                            var headerRow = new List<string> { "V\\H" };
                            headerRow.AddRange(hBreakpoints.Select(h => h.ToString()));
                            priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv((object[])headerRow.ToArray()));

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
                                        var cellResult = table.GetValue(hIndex, vIndex);
                                        rowValues.Add(cellResult.ReturnObject?.ToString() ?? "N/A");
                                    }
                                    catch
                                    {
                                        rowValues.Add("N/A");
                                    }
                                }
                                priceBreakPointCsv[listName].Add(CsvHelpers.WrapForCsv((object[])rowValues.ToArray()));
                            }
                        }
                    }
                }

                if (_cancelled) return new PriceTablesExportResult { WasCancelled = true };

                // Write files
                ReportProgress(90, 100, "Writing files...");

                File.WriteAllLines(priceListPath, priceListCsv);
                int filesWritten = 1;

                foreach (var kvp in priceBreakPointCsv)
                {
                    var sanitizedName = CsvHelpers.SanitizeFileName(kvp.Key);
                    var priceBpPath = Path.Combine(outputFolder, $"PriceBreakPoints_{sanitizedName}.csv");
                    File.WriteAllLines(priceBpPath, kvp.Value);
                    filesWritten++;
                }

                ReportProgress(100, 100, "Export complete");

                return new PriceTablesExportResult
                {
                    IsSuccess = true,
                    FolderPath = outputFolder,
                    FileCount = filesWritten,
                    SimpleListCount = priceListCsv.Count - 1,
                    BreakpointTableCount = priceBreakPointCsv.Count
                };
            }
            catch (Exception ex)
            {
                return new PriceTablesExportResult
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
    /// Result of price tables export operation.
    /// </summary>
    public class PriceTablesExportResult
    {
        public bool IsSuccess { get; set; }
        public bool WasCancelled { get; set; }
        public string ErrorMessage { get; set; }
        public string FolderPath { get; set; }
        public int FileCount { get; set; }
        public int SimpleListCount { get; set; }
        public int BreakpointTableCount { get; set; }
    }
}
