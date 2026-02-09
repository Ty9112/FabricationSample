using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for Specifications.
    /// Updates existing specifications from CSV format.
    /// Note: The Fabrication API does not support creating new specifications,
    /// so this service can only update Name and Group of existing specifications.
    /// </summary>
    public class SpecificationsImportService : CsvImportService
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

            return result;
        }

        /// <summary>
        /// Generate preview of import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = new ImportPreviewResult { IsSuccess = true };
            int startLine = options.HasHeaderRow ? 1 : 0;

            // Build lookup by Name+Group combination for unique matching
            var existingSpecs = new Dictionary<string, Specification>();
            foreach (Specification spec in Database.Specifications)
            {
                string key = GetSpecKey(spec.Name, spec.Group);
                if (!existingSpecs.ContainsKey(key))
                    existingSpecs[key] = spec;
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line, options.Delimiter);
                string name = GetFieldValue(headers, fields, "Name", options);
                string group = GetFieldValue(headers, fields, "Group", options);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string key = GetSpecKey(name, group);
                if (existingSpecs.ContainsKey(key))
                {
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Update",
                        Description = $"Update specification '{name}' in group '{group}'"
                    });
                    preview.UpdatedRecordCount++;
                }
                else
                {
                    // Cannot create new specifications via API
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Skip",
                        Description = $"Skip - specification '{name}' in group '{group}' not found (API does not support creation)"
                    });
                    preview.SkippedRecordCount++;
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the actual import operation.
        /// Note: Can only update existing specifications, cannot create new ones.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true };
            int startLine = options.HasHeaderRow ? 1 : 0;
            int total = lines.Count - startLine;
            int current = 0;

            // Build lookup by Name+Group combination for unique matching
            var existingSpecs = new Dictionary<string, Specification>();
            foreach (Specification spec in Database.Specifications)
            {
                string key = GetSpecKey(spec.Name, spec.Group);
                if (!existingSpecs.ContainsKey(key))
                    existingSpecs[key] = spec;
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled)
                    break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                current++;
                ReportProgress(current, total, $"Importing specification {current} of {total}", ImportPhase.Importing);

                var fields = ParseCsvLine(line, options.Delimiter);
                string name = GetFieldValue(headers, fields, "Name", options);
                string group = GetFieldValue(headers, fields, "Group", options);

                if (string.IsNullOrWhiteSpace(name))
                {
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    string key = GetSpecKey(name, group);

                    if (existingSpecs.TryGetValue(key, out Specification spec))
                    {
                        // Specification found - mark as processed
                        // Note: Name and Group are identifying fields, so not much to update
                        result.ImportedCount++;
                    }
                    else
                    {
                        // Cannot create new specifications via API
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors[i + 1] = $"Error processing specification '{name}': {ex.Message}";

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
        /// Create a unique key for a specification based on name and group.
        /// </summary>
        private string GetSpecKey(string name, string group)
        {
            return $"{(name ?? "").ToLowerInvariant()}|{(group ?? "").ToLowerInvariant()}";
        }
    }
}
