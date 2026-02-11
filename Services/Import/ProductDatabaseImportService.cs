using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for product database definitions and supplier IDs.
    /// Matches the CSV format produced by the Export Grid function on the Product Database tab.
    ///
    /// CSV Format:
    /// - Required columns: Id
    /// - Optional standard columns: Description, Finish, Specification, Material, ProductName,
    ///   Range, Size, Manufacturer, Source, InstallType, Group
    /// - Optional supplier columns: Any column matching a ProductSupplier name (e.g., "Ferguson", "OEM Code")
    /// </summary>
    public class ProductDatabaseImportService : CsvImportService
    {
        /// <summary>
        /// List of supplier names that should be processed as supplier ID columns.
        /// Set by the import handler before calling Validate/Preview/Import.
        /// </summary>
        public List<string> SupplierColumns { get; set; } = new List<string>();

        private static readonly HashSet<string> StandardFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "Description", "Finish", "Specification", "Material", "ProductName",
            "Range", "Size", "Manufacturer", "Source", "InstallType", "Group"
        };

        /// <summary>
        /// Validate column headers for product database import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "Id");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var id = GetFieldValue(headers, fields, "Id", CurrentOptions);
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Id cannot be empty"));
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// Generate preview of product database import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();
            int startLine = options.HasHeaderRow ? 1 : 0;

            // Build product definition lookup by Id
            var productLookup = new Dictionary<string, ProductDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in ProductDatabase.ProductDefinitions)
            {
                if (!string.IsNullOrEmpty(def.Id) && !productLookup.ContainsKey(def.Id))
                    productLookup[def.Id] = def;
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int lineNumber = i + 1;
                var fields = ParseCsvLine(line, options.Delimiter);

                var id = GetFieldValue(headers, fields, "Id", options);

                if (!productLookup.TryGetValue(id, out var existingDef))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Product definition not found: '{id}'"
                    });
                    continue;
                }

                var oldValues = new Dictionary<string, string>();
                var newValues = new Dictionary<string, string>();

                // Check standard fields for changes
                CheckFieldChange(headers, fields, options, existingDef.Description, "Description", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Finish, "Finish", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Specification, "Specification", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Material, "Material", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.ProductName, "ProductName", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Range, "Range", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Size, "Size", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Manufacturer, "Manufacturer", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.Source, "Source", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingDef.InstallType, "InstallType", oldValues, newValues);

                // Check supplier ID changes
                foreach (var supplierName in SupplierColumns)
                {
                    var csvValue = GetFieldValue(headers, fields, supplierName, options);
                    if (FindColumnIndex(headers, supplierName, options) < 0) continue;

                    if (existingDef.SupplierIds != null)
                    {
                        var supplierIdDef = existingDef.SupplierIds.FirstOrDefault(s =>
                            s.ProductSupplier != null &&
                            s.ProductSupplier.Name.Trim().Equals(supplierName, StringComparison.OrdinalIgnoreCase));

                        if (supplierIdDef != null)
                        {
                            string currentValue = supplierIdDef.Id ?? "";
                            if (!currentValue.Equals(csvValue ?? ""))
                            {
                                oldValues[supplierName] = currentValue;
                                newValues[supplierName] = csvValue ?? "";
                            }
                        }
                    }
                }

                if (oldValues.Count > 0)
                {
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Update product '{id}' ({oldValues.Count} field(s))",
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
                        Description = $"No changes for product '{id}'"
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the product database import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                int startLine = options.HasHeaderRow ? 1 : 0;
                int totalRows = lines.Count - startLine;
                int processedRows = 0;

                // Build product definition lookup by Id
                var productLookup = new Dictionary<string, ProductDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var def in ProductDatabase.ProductDefinitions)
                {
                    if (!string.IsNullOrEmpty(def.Id) && !productLookup.ContainsKey(def.Id))
                        productLookup[def.Id] = def;
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
                    var id = GetFieldValue(headers, fields, "Id", options);

                    if (!productLookup.TryGetValue(id, out var def))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    bool updated = false;

                    // Update standard fields
                    updated |= TryUpdateField(headers, fields, options, "Description", def.Description, v => def.Description = v);
                    updated |= TryUpdateField(headers, fields, options, "Finish", def.Finish, v => def.Finish = v);
                    updated |= TryUpdateField(headers, fields, options, "Specification", def.Specification, v => def.Specification = v);
                    updated |= TryUpdateField(headers, fields, options, "Material", def.Material, v => def.Material = v);
                    updated |= TryUpdateField(headers, fields, options, "ProductName", def.ProductName, v => def.ProductName = v);
                    updated |= TryUpdateField(headers, fields, options, "Range", def.Range, v => def.Range = v);
                    updated |= TryUpdateField(headers, fields, options, "Size", def.Size, v => def.Size = v);
                    updated |= TryUpdateField(headers, fields, options, "Manufacturer", def.Manufacturer, v => def.Manufacturer = v);
                    updated |= TryUpdateField(headers, fields, options, "Source", def.Source, v => def.Source = v);
                    updated |= TryUpdateField(headers, fields, options, "InstallType", def.InstallType, v => def.InstallType = v);

                    // Update supplier IDs
                    foreach (var supplierName in SupplierColumns)
                    {
                        if (FindColumnIndex(headers, supplierName, options) < 0) continue;

                        var csvValue = GetFieldValue(headers, fields, supplierName, options);

                        if (def.SupplierIds != null)
                        {
                            var supplierIdDef = def.SupplierIds.FirstOrDefault(s =>
                                s.ProductSupplier != null &&
                                s.ProductSupplier.Name.Trim().Equals(supplierName, StringComparison.OrdinalIgnoreCase));

                            if (supplierIdDef != null)
                            {
                                string currentValue = supplierIdDef.Id ?? "";
                                if (!currentValue.Equals(csvValue ?? ""))
                                {
                                    supplierIdDef.Id = csvValue ?? "";
                                    updated = true;
                                }
                            }
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

        /// <summary>
        /// Check if a field value differs between current and CSV data (for preview).
        /// </summary>
        private void CheckFieldChange(List<string> headers, List<string> fields, ImportOptions options,
            string currentValue, string fieldName, Dictionary<string, string> oldValues, Dictionary<string, string> newValues)
        {
            if (FindColumnIndex(headers, fieldName, options) < 0) return;

            var csvValue = GetFieldValue(headers, fields, fieldName, options);
            string current = currentValue ?? "";
            string newVal = csvValue ?? "";

            if (!current.Equals(newVal))
            {
                oldValues[fieldName] = current;
                newValues[fieldName] = newVal;
            }
        }

        /// <summary>
        /// Try to update a field if the CSV value differs from the current value.
        /// Returns true if the field was updated.
        /// </summary>
        private bool TryUpdateField(List<string> headers, List<string> fields, ImportOptions options,
            string fieldName, string currentValue, Action<string> setter)
        {
            if (FindColumnIndex(headers, fieldName, options) < 0) return false;

            var csvValue = GetFieldValue(headers, fields, fieldName, options);
            string current = currentValue ?? "";
            string newVal = csvValue ?? "";

            if (!current.Equals(newVal))
            {
                setter(newVal);
                return true;
            }
            return false;
        }
    }
}
