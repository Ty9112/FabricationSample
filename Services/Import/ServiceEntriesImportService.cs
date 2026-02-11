using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.LineWeights;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for service entries (layer configuration, service types).
    /// Matches the CSV format produced by ServiceEntriesExportService.
    ///
    /// CSV Format:
    /// - Required columns: Service Name, Service Type
    /// - Optional columns: Layer Tag 1, Layer Tag 2, Layer Color, Level Block, Size Block, Includes Insulation, Line Weight
    /// </summary>
    public class ServiceEntriesImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers for service entries import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "Service Name", "Service Type");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var serviceName = GetFieldValue(headers, fields, "Service Name", CurrentOptions);
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Service Name cannot be empty"));
                result.IsValid = false;
            }

            var serviceType = GetFieldValue(headers, fields, "Service Type", CurrentOptions);
            if (string.IsNullOrWhiteSpace(serviceType))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Service Type cannot be empty"));
                result.IsValid = false;
            }

            // Skip "No Entries" rows from the export
            if (serviceType.Equals("No Entries", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, $"Skipping 'No Entries' row for service '{serviceName}'"));
            }

            // Validate Layer Color if present
            var layerColorStr = GetFieldValue(headers, fields, "Layer Color", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(layerColorStr))
            {
                if (!TryParseInt(layerColorStr, out int layerColor) || layerColor < 0 || layerColor > 255)
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Invalid Layer Color '{layerColorStr}'. Must be 0-255. Will be skipped."));
                }
            }

            return result;
        }

        /// <summary>
        /// Generate preview of service entries import changes.
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

                var serviceName = GetFieldValue(headers, fields, "Service Name", options);
                var serviceType = GetFieldValue(headers, fields, "Service Type", options);

                // Skip "No Entries" rows
                if (serviceType.Equals("No Entries", StringComparison.OrdinalIgnoreCase))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Skip 'No Entries' row for service '{serviceName}'"
                    });
                    continue;
                }

                // Find matching service
                var service = FabDB.Services.FirstOrDefault(s =>
                    s.Name != null && s.Name.Trim().Equals(serviceName, StringComparison.OrdinalIgnoreCase));

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

                // Find matching entry by service type description (trim to handle leading spaces in API descriptions)
                var trimmedType = serviceType.Trim();
                var entry = service.ServiceEntries?.FirstOrDefault(e =>
                    e.ServiceType?.Description != null &&
                    e.ServiceType.Description.Trim().Equals(trimmedType, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Service entry type '{serviceType}' not found in service '{serviceName}'"
                    });
                    continue;
                }

                // Build change description
                var oldValues = new Dictionary<string, string>();
                var newValues = new Dictionary<string, string>();

                var layerTag1 = GetFieldValue(headers, fields, "Layer Tag 1", options);
                if (!string.IsNullOrEmpty(layerTag1))
                {
                    oldValues["Layer Tag 1"] = entry.LayerTag1 ?? "";
                    newValues["Layer Tag 1"] = layerTag1;
                }

                var layerTag2 = GetFieldValue(headers, fields, "Layer Tag 2", options);
                if (!string.IsNullOrEmpty(layerTag2))
                {
                    oldValues["Layer Tag 2"] = entry.LayerTag2 ?? "";
                    newValues["Layer Tag 2"] = layerTag2;
                }

                preview.UpdatedRecordCount++;
                preview.Changes.Add(new PreviewChange
                {
                    LineNumber = lineNumber,
                    ChangeType = "Update",
                    Description = $"Update '{serviceName}' / '{serviceType}'",
                    OldValues = oldValues,
                    NewValues = newValues
                });
            }

            return preview;
        }

        /// <summary>
        /// Perform the service entries import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
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
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var fields = ParseCsvLine(line, options.Delimiter);
                    var serviceName = GetFieldValue(headers, fields, "Service Name", options);
                    var serviceType = GetFieldValue(headers, fields, "Service Type", options);

                    // Skip "No Entries" rows
                    if (serviceType.Equals("No Entries", StringComparison.OrdinalIgnoreCase))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Find matching service
                    var service = FabDB.Services.FirstOrDefault(s =>
                        s.Name != null && s.Name.Trim().Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                    if (service == null)
                    {
                        result.SkippedCount++;
                        result.Errors[lineNumber] = $"Service not found: '{serviceName}'";
                        continue;
                    }

                    // Find matching entry by service type description (trim to handle leading spaces in API descriptions)
                    var trimmedType = serviceType.Trim();
                    var entry = service.ServiceEntries?.FirstOrDefault(e =>
                        e.ServiceType?.Description != null &&
                        e.ServiceType.Description.Trim().Equals(trimmedType, StringComparison.OrdinalIgnoreCase));

                    if (entry == null)
                    {
                        result.SkippedCount++;
                        result.Errors[lineNumber] = $"Service entry type '{serviceType}' not found in '{serviceName}'";
                        continue;
                    }

                    // Update writable properties
                    bool updated = false;

                    var layerTag1 = GetFieldValue(headers, fields, "Layer Tag 1", options);
                    if (!string.IsNullOrEmpty(layerTag1) && layerTag1 != entry.LayerTag1)
                    {
                        entry.LayerTag1 = layerTag1;
                        updated = true;
                    }

                    var layerTag2 = GetFieldValue(headers, fields, "Layer Tag 2", options);
                    if (!string.IsNullOrEmpty(layerTag2) && layerTag2 != entry.LayerTag2)
                    {
                        entry.LayerTag2 = layerTag2;
                        updated = true;
                    }

                    var layerColorStr = GetFieldValue(headers, fields, "Layer Color", options);
                    if (!string.IsNullOrEmpty(layerColorStr) && TryParseInt(layerColorStr, out int layerColor))
                    {
                        if (layerColor >= 0 && layerColor <= 255 && layerColor != entry.LayerColor)
                        {
                            entry.LayerColor = layerColor;
                            updated = true;
                        }
                    }

                    var levelBlock = GetFieldValue(headers, fields, "Level Block", options);
                    if (!string.IsNullOrEmpty(levelBlock) && !IsNaValue(levelBlock) && levelBlock != entry.LevelBlock)
                    {
                        entry.LevelBlock = levelBlock;
                        updated = true;
                    }

                    var sizeBlock = GetFieldValue(headers, fields, "Size Block", options);
                    if (!string.IsNullOrEmpty(sizeBlock) && !IsNaValue(sizeBlock) && sizeBlock != entry.SizeBlock)
                    {
                        entry.SizeBlock = sizeBlock;
                        updated = true;
                    }

                    var insulationStr = GetFieldValue(headers, fields, "Includes Insulation", options);
                    if (!string.IsNullOrEmpty(insulationStr))
                    {
                        bool includesInsulation = insulationStr.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                  insulationStr.Equals("True", StringComparison.OrdinalIgnoreCase);
                        if (includesInsulation != entry.IncludesInsulation)
                        {
                            entry.IncludesInsulation = includesInsulation;
                            updated = true;
                        }
                    }

                    var lineWeightStr = GetFieldValue(headers, fields, "Line Weight", options);
                    if (!string.IsNullOrEmpty(lineWeightStr))
                    {
                        if (Enum.TryParse<LineWeight.LineWeightEnum>(lineWeightStr, true, out var lineWeight))
                        {
                            if (lineWeight != entry.LineWeight.LineWeightValue)
                            {
                                entry.SetLineWeightValue(lineWeight);
                                updated = true;
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
    }
}
