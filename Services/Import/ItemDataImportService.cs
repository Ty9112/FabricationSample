using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for item data (read-only validation and preview).
    /// Matches the CSV format produced by ItemDataExportService.
    /// This service validates and previews only - item data modification via API is limited.
    /// It reports mismatches to help identify configuration differences between profiles.
    ///
    /// CSV Format:
    /// - Required columns: ServiceName, ButtonName, ItemFilePath
    /// - Optional columns: ProductListEntryName, ConditionDescription, GreaterThan, LessThanEqualTo
    /// </summary>
    public class ItemDataImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers for item data import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "ServiceName", "ButtonName", "ItemFilePath");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var serviceName = GetFieldValue(headers, fields, "ServiceName", CurrentOptions);
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                result.Errors.Add(new ValidationError(lineNumber, "ServiceName cannot be empty"));
                result.IsValid = false;
            }

            var buttonName = GetFieldValue(headers, fields, "ButtonName", CurrentOptions);
            if (string.IsNullOrWhiteSpace(buttonName))
            {
                result.Errors.Add(new ValidationError(lineNumber, "ButtonName cannot be empty"));
                result.IsValid = false;
            }

            var itemPath = GetFieldValue(headers, fields, "ItemFilePath", CurrentOptions);
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, "ItemFilePath is empty"));
            }

            return result;
        }

        /// <summary>
        /// Generate preview of item data comparison (read-only).
        /// Reports matches and mismatches between imported data and current configuration.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();
            int startLine = options.HasHeaderRow ? 1 : 0;

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int lineNumber = i + 1;
                var fields = ParseCsvLine(line, options.Delimiter);

                var serviceName = GetFieldValue(headers, fields, "ServiceName", options);
                var buttonName = GetFieldValue(headers, fields, "ButtonName", options);
                var itemPath = GetFieldValue(headers, fields, "ItemFilePath", options);
                var productListEntry = GetFieldValue(headers, fields, "ProductListEntryName", options);
                var conditionDesc = GetFieldValue(headers, fields, "ConditionDescription", options);

                // Find matching service
                var service = FabDB.Services.FirstOrDefault(s =>
                    s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                if (service == null)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Service not found: '{serviceName}'"
                    });
                    continue;
                }

                // Find matching button
                ServiceButton matchedButton = null;
                if (service.ServiceTemplate?.ServiceTabs != null)
                {
                    foreach (var tab in service.ServiceTemplate.ServiceTabs)
                    {
                        if (tab.ServiceButtons == null) continue;
                        matchedButton = tab.ServiceButtons.FirstOrDefault(b =>
                            b.Name != null && b.Name.Equals(buttonName, StringComparison.OrdinalIgnoreCase));
                        if (matchedButton != null) break;
                    }
                }

                if (matchedButton == null)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Button '{buttonName}' not found in service '{serviceName}'"
                    });
                    continue;
                }

                // Check if item path exists in button items
                bool itemFound = false;
                if (matchedButton.ServiceButtonItems != null && !string.IsNullOrWhiteSpace(itemPath))
                {
                    foreach (var sbItem in matchedButton.ServiceButtonItems)
                    {
                        if (sbItem.ItemPath != null &&
                            NormalizePath(sbItem.ItemPath).Equals(NormalizePath(itemPath), StringComparison.OrdinalIgnoreCase))
                        {
                            itemFound = true;
                            break;
                        }
                    }
                }

                if (itemFound)
                {
                    // Item matches - report as existing
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Match: '{serviceName}' / '{buttonName}' has item '{itemPath}'"
                    });
                }
                else
                {
                    // Item mismatch - report as warning
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Mismatch: '{serviceName}' / '{buttonName}' - item '{itemPath}' not found in current profile"
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the item data import operation (read-only - reports only).
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            // This is a read-only import - generate the preview as the result
            var preview = GeneratePreview(headers, lines, options);

            var result = new ImportResult
            {
                IsSuccess = true,
                FilePath = "",
                ImportedCount = preview.UpdatedRecordCount,
                SkippedCount = preview.SkippedRecordCount
            };

            result.Metadata["IsReadOnly"] = true;
            result.Metadata["MatchCount"] = preview.UpdatedRecordCount;
            result.Metadata["MismatchCount"] = preview.SkippedRecordCount;

            return result;
        }

        /// <summary>
        /// Normalize file path for comparison.
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Replace("\\", "/").TrimStart('.', '/');
        }
    }
}
