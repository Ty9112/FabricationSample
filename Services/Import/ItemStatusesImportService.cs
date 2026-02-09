using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for Item Statuses.
    /// Updates existing item statuses from CSV format.
    /// Note: The Fabrication API does not support creating new item statuses,
    /// so this service can only update existing statuses.
    /// </summary>
    public class ItemStatusesImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            // Name is required
            return ValidateRequiredColumns(headers, CurrentOptions, "Name");
        }

        /// <summary>
        /// Validate a single row of data.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            string name = GetFieldValue(headers, fields, "Name", CurrentOptions);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Name is required"));
                result.IsValid = false;
            }

            // Validate Color if provided
            string colorStr = GetFieldValue(headers, fields, "Color", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(colorStr))
            {
                if (!TryParseInt(colorStr, out int color) || color < 0 || color > 255)
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber, $"Invalid Color value '{colorStr}', must be 0-255"));
                }
            }

            // Validate Output if provided
            string outputStr = GetFieldValue(headers, fields, "Output", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(outputStr))
            {
                if (!bool.TryParse(outputStr, out _))
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber, $"Invalid Output value '{outputStr}', must be True or False"));
                }
            }

            return result;
        }

        /// <summary>
        /// Generate preview of import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = new ImportPreviewResult { IsSuccess = true };
            int startLine = options.HasHeaderRow ? 1 : 0;

            var existingStatuses = Database.ItemStatuses.ToDictionary(
                s => s.Name?.ToLowerInvariant() ?? "", s => s);

            for (int i = startLine; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line, options.Delimiter);
                string name = GetFieldValue(headers, fields, "Name", options);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (existingStatuses.ContainsKey(name.ToLowerInvariant()))
                {
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Update",
                        Description = $"Update item status '{name}'"
                    });
                    preview.UpdatedRecordCount++;
                }
                else
                {
                    // Cannot create new item statuses via API
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Skip",
                        Description = $"Skip - item status '{name}' not found (API does not support creation)"
                    });
                    preview.SkippedRecordCount++;
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the actual import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true };
            int startLine = options.HasHeaderRow ? 1 : 0;
            int total = lines.Count - startLine;
            int current = 0;

            var existingStatuses = Database.ItemStatuses.ToDictionary(
                s => s.Name?.ToLowerInvariant() ?? "", s => s);

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled)
                    break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                current++;
                ReportProgress(current, total, $"Importing status {current} of {total}", ImportPhase.Importing);

                var fields = ParseCsvLine(line, options.Delimiter);
                string name = GetFieldValue(headers, fields, "Name", options);

                if (string.IsNullOrWhiteSpace(name))
                {
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    ItemStatus status;
                    string nameKey = name.ToLowerInvariant();

                    if (existingStatuses.TryGetValue(nameKey, out status))
                    {
                        // Update existing status
                        UpdateItemStatus(status, headers, fields, options);
                        result.ImportedCount++;
                    }
                    else
                    {
                        // Cannot create new item statuses via API
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors[i + 1] = $"Error processing status '{name}': {ex.Message}";

                    if (options.StopOnFirstError)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = $"Import stopped due to error: {ex.Message}";
                        return result;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Update item status properties from CSV fields.
        /// </summary>
        private void UpdateItemStatus(ItemStatus status, List<string> headers, List<string> fields, ImportOptions options)
        {
            // LayerTag
            string layerTag = GetFieldValue(headers, fields, "LayerTag", options);
            if (!string.IsNullOrEmpty(layerTag))
            {
                status.LayerTag = layerTag;
            }

            // Color
            string colorStr = GetFieldValue(headers, fields, "Color", options);
            if (!string.IsNullOrWhiteSpace(colorStr) && TryParseInt(colorStr, out int color))
            {
                if (color >= 0 && color <= 255)
                {
                    status.Color = color;
                }
            }

            // Output
            string outputStr = GetFieldValue(headers, fields, "Output", options);
            if (!string.IsNullOrWhiteSpace(outputStr) && bool.TryParse(outputStr, out bool output))
            {
                status.Output = output;
            }
        }
    }
}
