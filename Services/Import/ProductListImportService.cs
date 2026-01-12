using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.Content;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for product list entries.
    /// Imports product list data into fabrication items.
    ///
    /// CSV Format:
    /// - Required columns: Name, Weight, Id
    /// - Optional columns: DIM:[DimensionName], OPT:[OptionName]
    /// - First row must be header
    ///
    /// Example:
    /// Name,Weight,Id,DIM:Width,DIM:Height,OPT:Color
    /// Product A,10.5,PROD-001,24,12,1
    /// Product B,15.2,PROD-002,30,18,2
    /// </summary>
    public class ProductListImportService : CsvImportService
    {
        private readonly dynamic _targetItem;

        /// <summary>
        /// Create a new product list import service for the specified item.
        /// </summary>
        /// <param name="targetItem">The item to import product list data into</param>
        public ProductListImportService(dynamic targetItem)
        {
            _targetItem = targetItem ?? throw new ArgumentNullException(nameof(targetItem));
        }

        /// <summary>
        /// Validate column headers for product list import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            // Check for required columns
            var result = ValidateRequiredColumns(headers, "Name", "Weight", "Id");
            if (!result.IsValid)
                return result;

            // Validate dimension and option columns
            var dimColumns = headers.Where(h => h.StartsWith("DIM:", StringComparison.OrdinalIgnoreCase)).ToList();
            var optColumns = headers.Where(h => h.StartsWith("OPT:", StringComparison.OrdinalIgnoreCase)).ToList();

            // Check that dimension names match item dimensions
            foreach (var dimCol in dimColumns)
            {
                var dimName = dimCol.Substring(4).Trim();
                var dimFromItem = _targetItem.Dimensions.FirstOrDefault(d => d.Name.Equals(dimName, StringComparison.OrdinalIgnoreCase));
                if (dimFromItem == null)
                {
                    result.Warnings.Add(new ValidationWarning(1,
                        $"Dimension '{dimName}' not found in item. This column will be ignored."));
                }
            }

            // Check that option names match item options
            foreach (var optCol in optColumns)
            {
                var optName = optCol.Substring(4).Trim();
                var optFromItem = _targetItem.Options.FirstOrDefault(o => o.Name.Equals(optName, StringComparison.OrdinalIgnoreCase));
                if (optFromItem == null)
                {
                    result.Warnings.Add(new ValidationWarning(1,
                        $"Option '{optName}' not found in item. This column will be ignored."));
                }
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate Name
            var name = GetFieldValue(headers, fields, "Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Name cannot be empty"));
                result.IsValid = false;
            }

            // Validate Weight
            var weightStr = GetFieldValue(headers, fields, "Weight");
            if (!TryParseDouble(weightStr, out double weight))
            {
                result.Errors.Add(new ValidationError(lineNumber, $"Invalid weight value: '{weightStr}'"));
                result.IsValid = false;
            }
            else if (weight < 0)
            {
                result.Errors.Add(new ValidationError(lineNumber, "Weight cannot be negative"));
                result.IsValid = false;
            }

            // Validate Id
            var id = GetFieldValue(headers, fields, "Id");
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Id cannot be empty"));
                result.IsValid = false;
            }

            // Validate dimension values
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].StartsWith("DIM:", StringComparison.OrdinalIgnoreCase))
                {
                    var dimName = headers[i].Substring(4).Trim();
                    var dimValue = i < fields.Count ? fields[i] : string.Empty;

                    if (!TryParseDouble(dimValue, out double val))
                    {
                        result.Errors.Add(new ValidationError(lineNumber,
                            $"Invalid dimension value for '{dimName}': '{dimValue}'"));
                        result.IsValid = false;
                    }
                }
            }

            // Validate option values
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].StartsWith("OPT:", StringComparison.OrdinalIgnoreCase))
                {
                    var optName = headers[i].Substring(4).Trim();
                    var optValue = i < fields.Count ? fields[i] : string.Empty;

                    if (!TryParseDouble(optValue, out double val))
                    {
                        result.Errors.Add(new ValidationError(lineNumber,
                            $"Invalid option value for '{optName}': '{optValue}'"));
                        result.IsValid = false;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Generate preview of product list import changes.
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
                var name = GetFieldValue(headers, fields, "Name");
                var id = GetFieldValue(headers, fields, "Id");

                // For product list, all entries are new since we're creating/replacing the list
                preview.NewRecordCount++;
                preview.Changes.Add(new PreviewChange
                {
                    LineNumber = lineNumber,
                    ChangeType = "New",
                    Description = $"Add product list entry: {name} (ID: {id})",
                    NewValues = new Dictionary<string, string>
                    {
                        { "Name", name },
                        { "Id", id },
                        { "Weight", GetFieldValue(headers, fields, "Weight") }
                    }
                });
            }

            return preview;
        }

        /// <summary>
        /// Perform the product list import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                // Find dimension and option column positions
                var dimPositions = new List<KeyValuePair<int, string>>();
                var optPositions = new List<KeyValuePair<int, string>>();

                for (int i = 0; i < headers.Count; i++)
                {
                    if (headers[i].StartsWith("DIM:", StringComparison.OrdinalIgnoreCase))
                    {
                        var dimName = headers[i].Substring(4).Trim();
                        dimPositions.Add(new KeyValuePair<int, string>(i, dimName));
                    }
                    else if (headers[i].StartsWith("OPT:", StringComparison.OrdinalIgnoreCase))
                    {
                        var optName = headers[i].Substring(4).Trim();
                        optPositions.Add(new KeyValuePair<int, string>(i, optName));
                    }
                }

                // Get column indices for required fields
                int nameIndex = FindColumnIndex(headers, "Name");
                int weightIndex = FindColumnIndex(headers, "Weight");
                int idIndex = FindColumnIndex(headers, "Id");

                // Create new product list
                var productList = new ItemProductList();

                // Process first data row to create template
                int startLine = options.HasHeaderRow ? 1 : 0;
                bool templateCreated = false;

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
                    var name = fields[nameIndex];
                    var weightStr = fields[weightIndex];
                    var id = fields[idIndex];

                    // Parse weight
                    if (!TryParseDouble(weightStr, out double weight))
                    {
                        result.Errors[lineNumber] = $"Invalid weight value: {weightStr}";
                        result.ErrorCount++;
                        if (options.StopOnFirstError)
                            break;
                        continue;
                    }

                    // Create data template on first valid row
                    if (!templateCreated)
                    {
                        var dimDefs = new List<ItemProductListDimensionDefinition>();
                        var optDefs = new List<ItemProductListOptionDefinition>();

                        // Add dimension definitions
                        foreach (var kvp in dimPositions)
                        {
                            var dimFromItem = _targetItem.Dimensions.FirstOrDefault(d =>
                                d.Name.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase));
                            if (dimFromItem != null)
                            {
                                dimDefs.Add(new ItemProductListDimensionDefinition(dimFromItem, true));
                            }
                        }

                        // Add option definitions
                        foreach (var kvp in optPositions)
                        {
                            var optFromItem = _targetItem.Options.FirstOrDefault(o =>
                                o.Name.Equals(kvp.Value, StringComparison.OrdinalIgnoreCase));
                            if (optFromItem != null)
                            {
                                optDefs.Add(new ItemProductListOptionDefinition(optFromItem, true));
                            }
                        }

                        // Create template
                        var template = new ItemProductListDataTemplate();
                        template.SetWeight(null);
                        template.SetDatabaseId(null);

                        foreach (var dimDef in dimDefs)
                        {
                            template.AddDimensionDefinition(dimDef, 0);
                        }

                        foreach (var optDef in optDefs)
                        {
                            template.AddOptionDefinition(optDef, 0);
                        }

                        productList.AddDataTemplate(template);
                        templateCreated = true;
                    }

                    // Create dimension entries
                    var dimEntries = new List<ItemProductListDimensionEntry>();
                    foreach (var dimDef in productList.Template.DimensionsDefinitions)
                    {
                        var kvp = dimPositions.FirstOrDefault(p =>
                            p.Value.Equals(dimDef.Name, StringComparison.OrdinalIgnoreCase));

                        if (kvp.Key >= 0 && kvp.Key < fields.Count)
                        {
                            if (TryParseDouble(fields[kvp.Key], out double dimValue))
                            {
                                dimEntries.Add(dimDef.CreateDimensionEntry(dimValue));
                            }
                        }
                    }

                    // Create option entries
                    var optEntries = new List<ItemProductListOptionEntry>();
                    foreach (var optDef in productList.Template.OptionsDefinitions)
                    {
                        var kvp = optPositions.FirstOrDefault(p =>
                            p.Value.Equals(optDef.Name, StringComparison.OrdinalIgnoreCase));

                        if (kvp.Key >= 0 && kvp.Key < fields.Count)
                        {
                            if (TryParseDouble(fields[kvp.Key], out double optValue))
                            {
                                optEntries.Add(optDef.CreateOptionEntry(optValue));
                            }
                        }
                    }

                    // Add row to product list
                    productList.AddRow(name, null, null, weight, null, null, id, null, null, null, dimEntries, optEntries);
                    result.ImportedCount++;

                    processedRows++;
                    int progress = 20 + (int)((processedRows / (double)totalRows) * 70);
                    ReportProgress(progress, 100, $"Imported {processedRows} of {totalRows} rows...", ImportPhase.Importing);
                }

                // Apply product list to item
                if (result.ImportedCount > 0)
                {
                    ReportProgress(90, 100, "Applying product list to item...", ImportPhase.Importing);
                    var createResult = ContentManager.CreateProductItem(_targetItem, productList);

                    if (createResult.Status != ResultStatus.Succeeded)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Failed to apply product list to item: {createResult.Status}";
                        return result;
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No valid rows to import";
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
