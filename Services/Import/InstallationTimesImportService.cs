using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for installation times (simple/product-based tables).
    /// Matches the InstallationProducts.csv format produced by InstallationTimesExportService.
    ///
    /// CSV Format:
    /// - Required columns: TableName, Id, LaborRate
    /// - Optional columns: TableGroup, Units, Status
    /// </summary>
    public class InstallationTimesImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers for installation times import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "TableName", "Id", "LaborRate");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var tableName = GetFieldValue(headers, fields, "TableName", CurrentOptions);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                result.Errors.Add(new ValidationError(lineNumber, "TableName cannot be empty"));
                result.IsValid = false;
            }

            var id = GetFieldValue(headers, fields, "Id", CurrentOptions);
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Id (DatabaseId) cannot be empty"));
                result.IsValid = false;
            }

            var laborRateStr = GetFieldValue(headers, fields, "LaborRate", CurrentOptions);
            if (!TryParseDouble(laborRateStr, out double laborRate))
            {
                result.Errors.Add(new ValidationError(lineNumber, $"Invalid LaborRate value: '{laborRateStr}'"));
                result.IsValid = false;
            }
            else if (laborRate < 0)
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, $"Negative LaborRate: {laborRate}"));
            }

            // Validate Status if present
            var status = GetFieldValue(headers, fields, "Status", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("POA", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("PriceOnApplication", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("Discon", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Unknown status value '{status}'. Valid: Active, POA, Discon. Defaulting to Active."));
                }
            }

            return result;
        }

        /// <summary>
        /// Generate preview of installation times import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();
            int startLine = options.HasHeaderRow ? 1 : 0;

            // Cache tables by name for performance
            var tableCache = new Dictionary<string, InstallationTimesTable>(StringComparer.OrdinalIgnoreCase);
            if (FabDB.InstallationTimesTable != null)
            {
                foreach (var table in FabDB.InstallationTimesTable)
                {
                    var simple = table as InstallationTimesTable;
                    if (simple != null && !(table is InstallationTimesTableWithBreakpoints))
                    {
                        // Use name as key (may have duplicates across groups, use first match)
                        var trimmedName = table.Name?.Trim() ?? "";
                        if (!tableCache.ContainsKey(trimmedName))
                            tableCache[trimmedName] = simple;
                    }
                }
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int lineNumber = i + 1;
                var fields = ParseCsvLine(line, options.Delimiter);

                var tableName = GetFieldValue(headers, fields, "TableName", options);
                var databaseId = GetFieldValue(headers, fields, "Id", options);
                var laborRateStr = GetFieldValue(headers, fields, "LaborRate", options);

                // Find table
                if (!tableCache.TryGetValue(tableName, out var installTable))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Installation table not found (or is breakpoint type): '{tableName}'"
                    });
                    continue;
                }

                // Find matching entry by database ID
                var existingEntry = installTable.Products.FirstOrDefault(p =>
                    p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                if (existingEntry != null)
                {
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Update '{tableName}' entry '{databaseId}'",
                        OldValues = new Dictionary<string, string> { { "LaborRate", existingEntry.Value.ToString() } },
                        NewValues = new Dictionary<string, string> { { "LaborRate", laborRateStr } }
                    });
                }
                else
                {
                    preview.NewRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "New",
                        Description = $"Add new entry '{databaseId}' to table '{tableName}'",
                        NewValues = new Dictionary<string, string>
                        {
                            { "DatabaseId", databaseId },
                            { "LaborRate", laborRateStr }
                        }
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the installation times import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                int startLine = options.HasHeaderRow ? 1 : 0;
                int totalRows = lines.Count - startLine;
                int processedRows = 0;

                // Cache tables by name
                var tableCache = new Dictionary<string, InstallationTimesTable>(StringComparer.OrdinalIgnoreCase);
                if (FabDB.InstallationTimesTable != null)
                {
                    foreach (var table in FabDB.InstallationTimesTable)
                    {
                        var simple = table as InstallationTimesTable;
                        if (simple != null && !(table is InstallationTimesTableWithBreakpoints))
                        {
                            if (!tableCache.ContainsKey(table.Name))
                                tableCache[table.Name] = simple;
                        }
                    }
                }

                for (int i = startLine; i < lines.Count; i++)
                {
                    if (IsCancelled)
                    {
                        result.WasCancelled = true;
                        result.IsSuccess = false;
                        return result;
                    }

                    int lineNumber = i + 1;
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var fields = ParseCsvLine(line, options.Delimiter);
                    var tableName = GetFieldValue(headers, fields, "TableName", options);
                    var databaseId = GetFieldValue(headers, fields, "Id", options);
                    var laborRateStr = GetFieldValue(headers, fields, "LaborRate", options);

                    if (!TryParseDouble(laborRateStr, out double laborRate))
                    {
                        result.Errors[lineNumber] = $"Invalid LaborRate value: {laborRateStr}";
                        result.ErrorCount++;
                        if (options.StopOnFirstError) break;
                        continue;
                    }

                    // Find table
                    if (!tableCache.TryGetValue(tableName, out var installTable))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Find existing entry
                    var existingEntry = installTable.Products.FirstOrDefault(p =>
                        p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                    if (existingEntry != null)
                    {
                        // Update existing entry
                        existingEntry.Value = laborRate;

                        // Update units if provided
                        var units = GetFieldValue(headers, fields, "Units", options);
                        if (!string.IsNullOrWhiteSpace(units))
                        {
                            existingEntry.CostedByLength = units.IndexOf("per(ft)", StringComparison.OrdinalIgnoreCase) >= 0;
                        }

                        // Update status if provided
                        var status = GetFieldValue(headers, fields, "Status", options);
                        if (!string.IsNullOrWhiteSpace(status))
                        {
                            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                existingEntry.Status = ProductEntryStatus.Active;
                            else if (status.Equals("POA", StringComparison.OrdinalIgnoreCase) ||
                                     status.Equals("PriceOnApplication", StringComparison.OrdinalIgnoreCase))
                                existingEntry.Status = ProductEntryStatus.PriceOnApplication;
                            else if (status.Equals("Discon", StringComparison.OrdinalIgnoreCase) ||
                                     status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase))
                                existingEntry.Status = ProductEntryStatus.Discontinued;
                        }

                        result.ImportedCount++;
                    }
                    else
                    {
                        // Try to add new entry
                        var addResult = installTable.AddEntry(databaseId);
                        if (addResult.Status == Autodesk.Fabrication.Results.ResultStatus.Succeeded)
                        {
                            var newEntry = installTable.Products.FirstOrDefault(p =>
                                p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                            if (newEntry != null)
                            {
                                newEntry.Value = laborRate;

                                var units = GetFieldValue(headers, fields, "Units", options);
                                if (!string.IsNullOrWhiteSpace(units))
                                    newEntry.CostedByLength = units.IndexOf("per(ft)", StringComparison.OrdinalIgnoreCase) >= 0;

                                var status = GetFieldValue(headers, fields, "Status", options);
                                if (!string.IsNullOrWhiteSpace(status))
                                {
                                    if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                        newEntry.Status = ProductEntryStatus.Active;
                                    else if (status.Equals("POA", StringComparison.OrdinalIgnoreCase) ||
                                             status.Equals("PriceOnApplication", StringComparison.OrdinalIgnoreCase))
                                        newEntry.Status = ProductEntryStatus.PriceOnApplication;
                                    else if (status.Equals("Discon", StringComparison.OrdinalIgnoreCase) ||
                                             status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase))
                                        newEntry.Status = ProductEntryStatus.Discontinued;
                                }

                                result.ImportedCount++;
                            }
                        }
                        else
                        {
                            result.Errors[lineNumber] = $"Failed to add entry '{databaseId}' to table '{tableName}'";
                            result.ErrorCount++;
                            if (options.StopOnFirstError) break;
                        }
                    }

                    processedRows++;
                    int progress = 20 + (int)((processedRows / (double)totalRows) * 70);
                    ReportProgress(progress, 100, $"Imported {processedRows} of {totalRows} rows...", ImportPhase.Importing);
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"Import failed: {ex.Message}";
                return result;
            }
        }
    }
}
