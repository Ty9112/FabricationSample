using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for supplier discounts.
    /// Matches the CSV format produced by btnExportSupplierDiscounts_Click.
    ///
    /// CSV Format:
    /// - Required columns: SupplierGroup, Code
    /// - Optional columns: Value, Description
    /// </summary>
    public class SupplierDiscountsImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers for supplier discounts import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "SupplierGroup", "Code");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var supplierGroup = GetFieldValue(headers, fields, "SupplierGroup", CurrentOptions);
            if (string.IsNullOrWhiteSpace(supplierGroup))
            {
                result.Errors.Add(new ValidationError(lineNumber, "SupplierGroup cannot be empty"));
                result.IsValid = false;
            }

            var code = GetFieldValue(headers, fields, "Code", CurrentOptions);
            if (string.IsNullOrWhiteSpace(code))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Code cannot be empty"));
                result.IsValid = false;
            }

            // Validate Value is numeric if present
            var valueStr = GetFieldValue(headers, fields, "Value", CurrentOptions);
            if (!string.IsNullOrEmpty(valueStr) && !TryParseDouble(valueStr, out _))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, $"Invalid Value: '{valueStr}'"));
            }

            return result;
        }

        /// <summary>
        /// Generate preview of supplier discounts import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();
            int startLine = options.HasHeaderRow ? 1 : 0;

            // Build lookup by supplier group name
            var supplierGroupLookup = new Dictionary<string, SupplierGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var sg in Database.SupplierGroups)
            {
                if (!string.IsNullOrEmpty(sg.Name) && !supplierGroupLookup.ContainsKey(sg.Name))
                    supplierGroupLookup[sg.Name] = sg;
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int lineNumber = i + 1;
                var fields = ParseCsvLine(line, options.Delimiter);

                var supplierGroupName = GetFieldValue(headers, fields, "SupplierGroup", options);
                var code = GetFieldValue(headers, fields, "Code", options);

                // Find supplier group
                if (!supplierGroupLookup.TryGetValue(supplierGroupName, out var supplierGroup))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Supplier group not found: '{supplierGroupName}'"
                    });
                    continue;
                }

                // Find existing discount by code
                var existingDiscount = supplierGroup.Discounts?.Discounts?
                    .FirstOrDefault(d => d.Code != null && d.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

                if (existingDiscount == null)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Discount code not found: '{code}' in group '{supplierGroupName}'"
                    });
                    continue;
                }

                var oldValues = new Dictionary<string, string>();
                var newValues = new Dictionary<string, string>();

                // Check Value change
                var valueStr = GetFieldValue(headers, fields, "Value", options);
                if (!string.IsNullOrEmpty(valueStr) && FindColumnIndex(headers, "Value", options) >= 0)
                {
                    if (TryParseDouble(valueStr, out double newValue) && Math.Abs(existingDiscount.Value - newValue) > 0.0001)
                    {
                        oldValues["Value"] = existingDiscount.Value.ToString();
                        newValues["Value"] = newValue.ToString();
                    }
                }

                // Check Description change
                var description = GetFieldValue(headers, fields, "Description", options);
                if (FindColumnIndex(headers, "Description", options) >= 0)
                {
                    string currentDesc = existingDiscount.Description ?? "";
                    if (currentDesc != description)
                    {
                        oldValues["Description"] = currentDesc;
                        newValues["Description"] = description;
                    }
                }

                if (oldValues.Count > 0)
                {
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Update discount '{code}' in '{supplierGroupName}' ({oldValues.Count} field(s))",
                        OldValues = oldValues,
                        NewValues = newValues
                    });
                }
                else
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"No changes for discount '{code}' in '{supplierGroupName}'"
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the supplier discounts import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                int startLine = options.HasHeaderRow ? 1 : 0;
                int totalRows = lines.Count - startLine;
                int processedRows = 0;

                // Build lookup
                var supplierGroupLookup = new Dictionary<string, SupplierGroup>(StringComparer.OrdinalIgnoreCase);
                foreach (var sg in Database.SupplierGroups)
                {
                    if (!string.IsNullOrEmpty(sg.Name) && !supplierGroupLookup.ContainsKey(sg.Name))
                        supplierGroupLookup[sg.Name] = sg;
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
                    var supplierGroupName = GetFieldValue(headers, fields, "SupplierGroup", options);
                    var code = GetFieldValue(headers, fields, "Code", options);

                    // Find supplier group
                    if (!supplierGroupLookup.TryGetValue(supplierGroupName, out var supplierGroup))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Find existing discount
                    var discount = supplierGroup.Discounts?.Discounts?
                        .FirstOrDefault(d => d.Code != null && d.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

                    if (discount == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    bool updated = false;

                    // Update Value
                    var valueStr = GetFieldValue(headers, fields, "Value", options);
                    if (!string.IsNullOrEmpty(valueStr) && FindColumnIndex(headers, "Value", options) >= 0)
                    {
                        if (TryParseDouble(valueStr, out double newValue) && Math.Abs(discount.Value - newValue) > 0.0001)
                        {
                            discount.Value = newValue;
                            updated = true;
                        }
                    }

                    // Update Description
                    var description = GetFieldValue(headers, fields, "Description", options);
                    if (FindColumnIndex(headers, "Description", options) >= 0)
                    {
                        if ((discount.Description ?? "") != description)
                        {
                            discount.Description = description;
                            updated = true;
                        }
                    }

                    if (updated)
                        result.ImportedCount++;
                    else
                        result.SkippedCount++;

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
