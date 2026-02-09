using System;
using System.Collections.Generic;
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
    /// - Optional columns: DiscountCode, Units, Status
    /// - First row must be header
    ///
    /// Example:
    /// DatabaseId,Cost,DiscountCode,Units,Status
    /// PROD-001,125.50,A,per(ft),Active
    /// PROD-002,89.00,B,(each),Active
    /// </summary>
    public class PriceTableImportService : CsvImportService
    {
        private readonly PriceList _priceList;

        /// <summary>
        /// Create a new price table import service for the specified price list.
        /// </summary>
        /// <param name="priceList">The price list to import data into</param>
        public PriceTableImportService(PriceList priceList)
        {
            _priceList = priceList ?? throw new ArgumentNullException(nameof(priceList));
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

            // Validate DatabaseId
            var databaseId = GetFieldValue(headers, fields, "DatabaseId", CurrentOptions);
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                result.Errors.Add(new ValidationError(lineNumber, "DatabaseId cannot be empty"));
                result.IsValid = false;
            }

            // Validate Cost
            var costStr = GetFieldValue(headers, fields, "Cost", CurrentOptions);
            if (!TryParseDouble(costStr, out double cost))
            {
                result.Errors.Add(new ValidationError(lineNumber, $"Invalid cost value: '{costStr}'"));
                result.IsValid = false;
            }
            else if (cost < 0)
            {
                result.Errors.Add(new ValidationError(lineNumber, "Cost cannot be negative"));
                result.IsValid = false;
            }

            // Validate Status if present
            var status = GetFieldValue(headers, fields, "Status", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("POA", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("Discon", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Unknown status value '{status}'. Valid values: Active, POA, Discon. Defaulting to Active."));
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
                var cost = GetFieldValue(headers, fields, "Cost", options);

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
                                { "Cost", cost }
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
                            { "Cost", cost }
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

                    // Parse cost
                    if (!TryParseDouble(costStr, out double cost))
                    {
                        result.Errors[lineNumber] = $"Invalid cost value: {costStr}";
                        result.ErrorCount++;
                        if (options.StopOnFirstError)
                            break;
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

                            // Update discount code if provided
                            if (discountIndex >= 0 && discountIndex < fields.Count)
                            {
                                var discountCode = fields[discountIndex];
                                if (!string.IsNullOrWhiteSpace(discountCode))
                                    existingEntry.DiscountCode = discountCode;
                            }

                            // Update units if provided
                            if (unitsIndex >= 0 && unitsIndex < fields.Count)
                            {
                                var units = fields[unitsIndex];
                                if (!string.IsNullOrWhiteSpace(units))
                                {
                                    existingEntry.CostedByLength = units.IndexOf("per(ft)", StringComparison.OrdinalIgnoreCase) >= 0;
                                }
                            }

                            // Update status if provided
                            if (statusIndex >= 0 && statusIndex < fields.Count)
                            {
                                var status = fields[statusIndex];
                                if (!string.IsNullOrWhiteSpace(status))
                                {
                                    if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                        existingEntry.Status = ProductEntryStatus.Active;
                                    else if (status.Equals("POA", StringComparison.OrdinalIgnoreCase))
                                        existingEntry.Status = ProductEntryStatus.PriceOnApplication;
                                    else if (status.Equals("Discon", StringComparison.OrdinalIgnoreCase))
                                        existingEntry.Status = ProductEntryStatus.Discontinued;
                                }
                            }

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

                                // Set discount code if provided
                                if (discountIndex >= 0 && discountIndex < fields.Count)
                                {
                                    var discountCode = fields[discountIndex];
                                    if (!string.IsNullOrWhiteSpace(discountCode))
                                        newEntry.DiscountCode = discountCode;
                                }

                                // Set units if provided
                                if (unitsIndex >= 0 && unitsIndex < fields.Count)
                                {
                                    var units = fields[unitsIndex];
                                    if (!string.IsNullOrWhiteSpace(units))
                                    {
                                        newEntry.CostedByLength = units.IndexOf("per(ft)", StringComparison.OrdinalIgnoreCase) >= 0;
                                    }
                                }

                                // Set status if provided
                                if (statusIndex >= 0 && statusIndex < fields.Count)
                                {
                                    var status = fields[statusIndex];
                                    if (!string.IsNullOrWhiteSpace(status))
                                    {
                                        if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                                            newEntry.Status = ProductEntryStatus.Active;
                                        else if (status.Equals("POA", StringComparison.OrdinalIgnoreCase))
                                            newEntry.Status = ProductEntryStatus.PriceOnApplication;
                                        else if (status.Equals("Discon", StringComparison.OrdinalIgnoreCase))
                                            newEntry.Status = ProductEntryStatus.Discontinued;
                                    }
                                }

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
    }
}
