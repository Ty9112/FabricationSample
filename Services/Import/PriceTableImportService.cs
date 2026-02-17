using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for price list entries.
    /// Imports or updates product prices in price lists.
    ///
    /// CSV Format:
    /// - Required columns: DatabaseId, Cost
    /// - Optional columns: DiscountCode, Units, Status, Date
    /// - First row must be header
    /// - Rows with unparseable values in Cost, Status, Units, Discount, or Date are skipped
    ///
    /// Example:
    /// DatabaseId,Cost,DiscountCode,Units,Status,Date
    /// PROD-001,125.50,A,per(ft),Active,15/01/2024
    /// PROD-002,89.00,B,(each),Active,20/03/2024
    /// </summary>
    public class PriceTableImportService : CsvImportService
    {
        private readonly PriceList _priceList;
        private readonly SupplierGroup _supplierGroup;

        /// <summary>
        /// Date formats accepted during import (dd/MM/yyyy primary, others as fallback).
        /// </summary>
        private static readonly string[] DateFormats = new[]
        {
            "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yyyy",
            "MM/dd/yyyy", "M/dd/yyyy", "MM/d/yyyy", "M/d/yyyy",
            "yyyy-MM-dd"
        };

        /// <summary>
        /// Create a new price table import service for the specified price list.
        /// </summary>
        /// <param name="priceList">The price list to import data into</param>
        /// <param name="supplierGroup">The supplier group for discount code validation (optional)</param>
        public PriceTableImportService(PriceList priceList, SupplierGroup supplierGroup = null)
        {
            _priceList = priceList ?? throw new ArgumentNullException(nameof(priceList));
            _supplierGroup = supplierGroup;
        }

        /// <summary>
        /// Validate column headers for price list import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            // Check for required columns using mapped names if available
            var result = ValidateRequiredColumns(headers, CurrentOptions, "DatabaseId", "Cost");
            return result;
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate DatabaseId -- this is the only hard error (row cannot be identified without it)
            var databaseId = GetFieldValue(headers, fields, "DatabaseId", CurrentOptions);
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                result.Errors.Add(new ValidationError(lineNumber, "DatabaseId cannot be empty"));
                result.IsValid = false;
                return result;
            }

            // All other field issues are warnings -- row will be skipped during import
            // Validate Cost
            var costStr = GetFieldValue(headers, fields, "Cost", CurrentOptions);
            if (IsNaValue(costStr))
            {
                // N/A is valid -- row will be skipped during import
            }
            else if (!string.IsNullOrWhiteSpace(costStr) && !TryParseDouble(costStr, out double cost))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber,
                    $"Unparseable cost value '{costStr}' — row will be skipped during import."));
            }
            else if (TryParseDouble(costStr, out double costVal) && costVal < 0)
            {
                result.Warnings.Add(new ValidationWarning(lineNumber,
                    $"Negative cost value ({costVal}) — row will be skipped during import."));
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
                        $"Unknown status '{status}' — field will be ignored. Valid values: Active, POA, Discon."));
                }
            }

            // Validate Date if present
            var dateStr = GetFieldValue(headers, fields, "Date", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(dateStr) && !IsNaValue(dateStr) &&
                !dateStr.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                if (!DateTime.TryParseExact(dateStr, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _))
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Unparseable date '{dateStr}' — field will be ignored."));
                }
            }

            // Validate DiscountCode if present and supplier group available
            var discountCode = GetFieldValue(headers, fields, "DiscountCode", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(discountCode) && !IsNaValue(discountCode) && _supplierGroup != null)
            {
                var discount = _supplierGroup.Discounts.Discounts.FirstOrDefault(x => x.Code == discountCode);
                if (discount == null)
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Discount code '{discountCode}' not found in supplier group — field will be ignored."));
                }
            }

            return result;
        }

        /// <summary>
        /// Generate preview of price list import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();

            int startLine = options.HasHeaderRow ? 1 : 0;

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                int lineNumber = i + 1;
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line, options.Delimiter);
                var databaseId = GetFieldValue(headers, fields, "DatabaseId", options);
                var costStr = GetFieldValue(headers, fields, "Cost", options);

                // Skip N/A cost values
                if (IsNaValue(costStr))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Skip {databaseId} — cost is N/A"
                    });
                    continue;
                }

                // Skip rows with unparseable cost
                if (!TryParseDouble(costStr, out double costVal) || costVal < 0)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Skip {databaseId} — invalid cost value '{costStr}'"
                    });
                    continue;
                }

                // Check if entry already exists
                var existingEntry = _priceList.Products.FirstOrDefault(p =>
                    p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                if (existingEntry != null)
                {
                    if (options.UpdateExisting)
                    {
                        preview.UpdatedRecordCount++;
                        preview.Changes.Add(new PreviewChange
                        {
                            LineNumber = lineNumber,
                            ChangeType = "Update",
                            Description = $"Update price for {databaseId}",
                            OldValues = new Dictionary<string, string>
                            {
                                { "Cost", existingEntry.Value.ToString() }
                            },
                            NewValues = new Dictionary<string, string>
                            {
                                { "Cost", costStr }
                            }
                        });
                    }
                    else
                    {
                        preview.SkippedRecordCount++;
                        preview.Changes.Add(new PreviewChange
                        {
                            LineNumber = lineNumber,
                            ChangeType = "Skip",
                            Description = $"Skip existing entry: {databaseId}"
                        });
                    }
                }
                else
                {
                    preview.NewRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "New",
                        Description = $"Add new price entry: {databaseId}",
                        NewValues = new Dictionary<string, string>
                        {
                            { "DatabaseId", databaseId },
                            { "Cost", costStr }
                        }
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the price list import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                // Get column indices (using mapped names if available)
                int dbIdIndex = FindColumnIndex(headers, "DatabaseId", options);
                int costIndex = FindColumnIndex(headers, "Cost", options);
                int discountIndex = FindColumnIndex(headers, "DiscountCode", options);
                int unitsIndex = FindColumnIndex(headers, "Units", options);
                int statusIndex = FindColumnIndex(headers, "Status", options);
                int dateIndex = FindColumnIndex(headers, "Date", options);

                int startLine = options.HasHeaderRow ? 1 : 0;
                int totalRows = lines.Count - startLine;
                int processedRows = 0;

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

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var fields = ParseCsvLine(line, options.Delimiter);

                    // Get field values
                    var databaseId = fields[dbIdIndex];
                    var costStr = fields[costIndex];

                    // Skip N/A cost values
                    if (IsNaValue(costStr))
                    {
                        result.SkippedCount++;
                        processedRows++;
                        continue;
                    }

                    // Skip rows with unparseable or negative cost
                    if (!TryParseDouble(costStr, out double cost) || cost < 0)
                    {
                        result.SkippedCount++;
                        processedRows++;
                        continue;
                    }

                    // Check if entry exists
                    var existingEntry = _priceList.Products.FirstOrDefault(p =>
                        p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                    if (existingEntry != null)
                    {
                        if (options.UpdateExisting)
                        {
                            // Update existing entry
                            existingEntry.Value = cost;
                            ApplyOptionalFields(existingEntry, fields, discountIndex, unitsIndex, statusIndex, dateIndex);
                            result.ImportedCount++;
                        }
                        else
                        {
                            result.SkippedCount++;
                        }
                    }
                    else
                    {
                        // Add new entry
                        var addResult = _priceList.AddEntry(databaseId);
                        if (addResult.Status == Autodesk.Fabrication.Results.ResultStatus.Succeeded)
                        {
                            // Find the newly added entry
                            var newEntry = _priceList.Products.FirstOrDefault(p =>
                                p.DatabaseId.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

                            if (newEntry != null)
                            {
                                newEntry.Value = cost;
                                ApplyOptionalFields(newEntry, fields, discountIndex, unitsIndex, statusIndex, dateIndex);
                                result.ImportedCount++;
                            }
                        }
                        else
                        {
                            result.Errors[lineNumber] = $"Failed to add entry: {addResult.Message}";
                            result.ErrorCount++;
                            if (options.StopOnFirstError)
                                break;
                        }
                    }

                    processedRows++;
                    int progress = 20 + (int)((processedRows / (double)totalRows) * 70);
                    ReportProgress(progress, 100, $"Imported {processedRows} of {totalRows} rows...", ImportPhase.Importing);
                }

                // Save changes to database
                if (result.ImportedCount > 0)
                {
                    ReportProgress(95, 100, "Saving changes to database...", ImportPhase.Importing);
                    // Note: Database.Save() should be called by the consuming code
                    // as it may want to batch multiple operations
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

        /// <summary>
        /// Apply optional field values (discount, units, status, date) to a product entry.
        /// Invalid or unparseable values are silently skipped per field — the row is still imported.
        /// </summary>
        private void ApplyOptionalFields(ProductEntry entry, List<string> fields,
            int discountIndex, int unitsIndex, int statusIndex, int dateIndex)
        {
            // Set discount code if provided and valid against supplier group
            if (discountIndex >= 0 && discountIndex < fields.Count)
            {
                var discountCode = fields[discountIndex];
                if (!string.IsNullOrWhiteSpace(discountCode) && !IsNaValue(discountCode))
                {
                    if (_supplierGroup != null)
                    {
                        // Validate against supplier group's discount list
                        var discount = _supplierGroup.Discounts.Discounts
                            .FirstOrDefault(x => x.Code == discountCode);
                        if (discount != null)
                            entry.DiscountCode = discount.Code;
                    }
                    else
                    {
                        // No supplier group available — set directly
                        entry.DiscountCode = discountCode;
                    }
                }
            }

            // Set units if provided
            if (unitsIndex >= 0 && unitsIndex < fields.Count)
            {
                var units = fields[unitsIndex];
                if (!string.IsNullOrWhiteSpace(units))
                {
                    entry.CostedByLength = units.IndexOf("per(ft)", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            // Set status if provided and valid
            if (statusIndex >= 0 && statusIndex < fields.Count)
            {
                var status = fields[statusIndex];
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                        entry.Status = ProductEntryStatus.Active;
                    else if (status.Equals("POA", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("PriceOnApplication", StringComparison.OrdinalIgnoreCase))
                        entry.Status = ProductEntryStatus.PriceOnApplication;
                    else if (status.Equals("Discon", StringComparison.OrdinalIgnoreCase) ||
                             status.Equals("Discontinued", StringComparison.OrdinalIgnoreCase))
                        entry.Status = ProductEntryStatus.Discontinued;
                }
            }

            // Set date if provided and parseable
            if (dateIndex >= 0 && dateIndex < fields.Count)
            {
                var dateStr = fields[dateIndex];
                if (!string.IsNullOrWhiteSpace(dateStr) && !IsNaValue(dateStr) &&
                    !dateStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParseExact(dateStr, DateFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime dateVal))
                    {
                        entry.Date = dateVal;
                    }
                    // Unparseable dates are silently ignored
                }
            }
        }
    }
}
