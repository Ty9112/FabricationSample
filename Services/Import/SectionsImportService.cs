using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for database sections.
    /// Matches the CSV format produced by SectionsExportService.
    /// Creates new sections if they don't exist, or updates existing ones.
    ///
    /// CSV Format:
    /// - Required columns: Description
    /// - Optional columns: Group, Index, DifficultyFactor, RetroFitted, RetroFittedPercentage,
    ///   LayerTag, FloorLevel, SlabLevel, ColorR, ColorG, ColorB
    /// </summary>
    public class SectionsImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers for sections import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            return ValidateRequiredColumns(headers, CurrentOptions, "Description");
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            var description = GetFieldValue(headers, fields, "Description", CurrentOptions);
            if (string.IsNullOrWhiteSpace(description))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Description cannot be empty"));
                result.IsValid = false;
            }

            // Validate numeric fields if present
            var indexStr = GetFieldValue(headers, fields, "Index", CurrentOptions);
            if (!string.IsNullOrEmpty(indexStr) && !TryParseInt(indexStr, out _))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, $"Invalid Index value: '{indexStr}'"));
            }

            var diffFactorStr = GetFieldValue(headers, fields, "DifficultyFactor", CurrentOptions);
            if (!string.IsNullOrEmpty(diffFactorStr) && !TryParseDouble(diffFactorStr, out _))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, $"Invalid DifficultyFactor value: '{diffFactorStr}'"));
            }

            return result;
        }

        /// <summary>
        /// Generate preview of sections import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = ImportPreviewResult.Success();
            int startLine = options.HasHeaderRow ? 1 : 0;

            // Build lookups for flexible matching
            var sectionByKey = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
            var sectionsByDescription = new Dictionary<string, List<Section>>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in Database.Sections)
            {
                string key = GetSectionKey(section.Description, section.Group);
                if (!sectionByKey.ContainsKey(key))
                    sectionByKey[key] = section;

                // Also track by description only for fallback matching
                string desc = section.Description ?? "";
                if (!sectionsByDescription.ContainsKey(desc))
                    sectionsByDescription[desc] = new List<Section>();
                sectionsByDescription[desc].Add(section);
            }

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled) break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                int lineNumber = i + 1;
                var fields = ParseCsvLine(line, options.Delimiter);

                var description = GetFieldValue(headers, fields, "Description", options);
                var group = GetFieldValue(headers, fields, "Group", options);

                // Try to find matching section with flexible matching
                var existingSection = FindMatchingSection(description, group, sectionByKey, sectionsByDescription);

                if (existingSection == null)
                {
                    // Section doesn't exist - will be created
                    preview.NewRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "New",
                        Description = $"Create new section: '{description}' (Group: '{group}')"
                    });
                    continue;
                }

                var oldValues = new Dictionary<string, string>();
                var newValues = new Dictionary<string, string>();

                // Check each field for changes
                CheckFieldChange(headers, fields, options, existingSection.Index.ToString(), "Index", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.DifficultyFactor.ToString(), "DifficultyFactor", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.RetroFitted ? "True" : "False", "RetroFitted", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.RetroFittedPercentage.ToString(), "RetroFittedPercentage", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.LayerTag ?? "", "LayerTag", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.FloorLevel.ToString(), "FloorLevel", oldValues, newValues);
                CheckFieldChange(headers, fields, options, existingSection.SlabLevel.ToString(), "SlabLevel", oldValues, newValues);

                // Check color
                CheckColorChange(headers, fields, options, existingSection.Color, oldValues, newValues);

                if (oldValues.Count > 0)
                {
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Update section '{description}' ({oldValues.Count} field(s))",
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
                        Description = $"No changes for section '{description}'"
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the sections import operation.
        /// </summary>
        protected override ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options)
        {
            var result = new ImportResult { IsSuccess = true, FilePath = "" };

            try
            {
                int startLine = options.HasHeaderRow ? 1 : 0;
                int totalRows = lines.Count - startLine;
                int processedRows = 0;

                // Build lookups for flexible matching
                var sectionByKey = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
                var sectionsByDescription = new Dictionary<string, List<Section>>(StringComparer.OrdinalIgnoreCase);

                foreach (var section in Database.Sections)
                {
                    string key = GetSectionKey(section.Description, section.Group);
                    if (!sectionByKey.ContainsKey(key))
                        sectionByKey[key] = section;

                    string desc = section.Description ?? "";
                    if (!sectionsByDescription.ContainsKey(desc))
                        sectionsByDescription[desc] = new List<Section>();
                    sectionsByDescription[desc].Add(section);
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
                    var description = GetFieldValue(headers, fields, "Description", options);
                    var group = GetFieldValue(headers, fields, "Group", options);

                    // Try flexible matching
                    var section = FindMatchingSection(description, group, sectionByKey, sectionsByDescription);

                    bool isNewSection = false;
                    if (section == null)
                    {
                        // Create new section
                        var addResult = Database.AddSection(description, group ?? "");
                        if (addResult.Status == ResultStatus.Succeeded)
                        {
                            section = addResult.ReturnObject as Section;
                            isNewSection = true;

                            // Add to lookups for subsequent rows
                            if (section != null)
                            {
                                string newKey = GetSectionKey(section.Description, section.Group);
                                if (!sectionByKey.ContainsKey(newKey))
                                    sectionByKey[newKey] = section;

                                string desc = section.Description ?? "";
                                if (!sectionsByDescription.ContainsKey(desc))
                                    sectionsByDescription[desc] = new List<Section>();
                                sectionsByDescription[desc].Add(section);
                            }
                        }
                        else
                        {
                            result.SkippedCount++;
                            result.Errors[lineNumber] = $"Failed to create section: {addResult.Message}";
                            continue;
                        }
                    }

                    if (section == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    bool updated = isNewSection; // New sections count as updated

                    // Update Index
                    var indexStr = GetFieldValue(headers, fields, "Index", options);
                    if (!string.IsNullOrEmpty(indexStr) && FindColumnIndex(headers, "Index", options) >= 0)
                    {
                        if (TryParseInt(indexStr, out int indexVal) && section.Index != indexVal)
                        {
                            section.Index = indexVal;
                            updated = true;
                        }
                    }

                    // Update DifficultyFactor
                    var diffStr = GetFieldValue(headers, fields, "DifficultyFactor", options);
                    if (!string.IsNullOrEmpty(diffStr) && FindColumnIndex(headers, "DifficultyFactor", options) >= 0)
                    {
                        if (TryParseDouble(diffStr, out double diffVal) && Math.Abs(section.DifficultyFactor - diffVal) > 0.0001)
                        {
                            section.DifficultyFactor = diffVal;
                            updated = true;
                        }
                    }

                    // Update RetroFitted
                    var retroStr = GetFieldValue(headers, fields, "RetroFitted", options);
                    if (!string.IsNullOrEmpty(retroStr) && FindColumnIndex(headers, "RetroFitted", options) >= 0)
                    {
                        bool retroVal = retroStr.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                       retroStr.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                       retroStr == "1";
                        if (section.RetroFitted != retroVal)
                        {
                            section.RetroFitted = retroVal;
                            updated = true;
                        }
                    }

                    // Update RetroFittedPercentage
                    var retroPctStr = GetFieldValue(headers, fields, "RetroFittedPercentage", options);
                    if (!string.IsNullOrEmpty(retroPctStr) && FindColumnIndex(headers, "RetroFittedPercentage", options) >= 0)
                    {
                        if (TryParseDouble(retroPctStr, out double retroPctVal) && Math.Abs(section.RetroFittedPercentage - retroPctVal) > 0.0001)
                        {
                            section.RetroFittedPercentage = retroPctVal;
                            updated = true;
                        }
                    }

                    // Update LayerTag
                    var layerTag = GetFieldValue(headers, fields, "LayerTag", options);
                    if (FindColumnIndex(headers, "LayerTag", options) >= 0)
                    {
                        if ((section.LayerTag ?? "") != layerTag)
                        {
                            section.LayerTag = layerTag;
                            updated = true;
                        }
                    }

                    // Update FloorLevel
                    var floorStr = GetFieldValue(headers, fields, "FloorLevel", options);
                    if (!string.IsNullOrEmpty(floorStr) && FindColumnIndex(headers, "FloorLevel", options) >= 0)
                    {
                        if (TryParseDouble(floorStr, out double floorVal) && Math.Abs(section.FloorLevel - floorVal) > 0.0001)
                        {
                            section.FloorLevel = floorVal;
                            updated = true;
                        }
                    }

                    // Update SlabLevel
                    var slabStr = GetFieldValue(headers, fields, "SlabLevel", options);
                    if (!string.IsNullOrEmpty(slabStr) && FindColumnIndex(headers, "SlabLevel", options) >= 0)
                    {
                        if (TryParseDouble(slabStr, out double slabVal) && Math.Abs(section.SlabLevel - slabVal) > 0.0001)
                        {
                            section.SlabLevel = slabVal;
                            updated = true;
                        }
                    }

                    // Update Color
                    var colorRStr = GetFieldValue(headers, fields, "ColorR", options);
                    var colorGStr = GetFieldValue(headers, fields, "ColorG", options);
                    var colorBStr = GetFieldValue(headers, fields, "ColorB", options);
                    if (FindColumnIndex(headers, "ColorR", options) >= 0 &&
                        FindColumnIndex(headers, "ColorG", options) >= 0 &&
                        FindColumnIndex(headers, "ColorB", options) >= 0)
                    {
                        if (TryParseInt(colorRStr, out int r) &&
                            TryParseInt(colorGStr, out int g) &&
                            TryParseInt(colorBStr, out int b))
                        {
                            r = Math.Max(0, Math.Min(255, r));
                            g = Math.Max(0, Math.Min(255, g));
                            b = Math.Max(0, Math.Min(255, b));

                            if (section.Color.R != r || section.Color.G != g || section.Color.B != b)
                            {
                                section.Color = Color.FromArgb(r, g, b);
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

        /// <summary>
        /// Create a unique key for section lookup (Description + Group).
        /// </summary>
        private string GetSectionKey(string description, string group)
        {
            return $"{description ?? ""}|{group ?? ""}";
        }

        /// <summary>
        /// Find a matching section using flexible matching strategy:
        /// 1. First try exact match by Description + Group
        /// 2. If no match, try Description only (if only one section has that description)
        /// 3. If no match, try Description only with any group (take first match)
        /// </summary>
        private Section FindMatchingSection(
            string description,
            string group,
            Dictionary<string, Section> sectionByKey,
            Dictionary<string, List<Section>> sectionsByDescription)
        {
            // Strategy 1: Exact match by Description + Group
            string key = GetSectionKey(description, group);
            if (sectionByKey.TryGetValue(key, out var exactMatch))
                return exactMatch;

            // Strategy 2: Match by Description only
            if (sectionsByDescription.TryGetValue(description ?? "", out var matchingByDesc) && matchingByDesc.Count > 0)
            {
                // If only one section has this description, use it regardless of group
                if (matchingByDesc.Count == 1)
                    return matchingByDesc[0];

                // Multiple sections with same description - try to find one with empty/null group
                var emptyGroupMatch = matchingByDesc.FirstOrDefault(s => string.IsNullOrEmpty(s.Group));
                if (emptyGroupMatch != null)
                    return emptyGroupMatch;

                // Fall back to first match
                return matchingByDesc[0];
            }

            return null;
        }

        /// <summary>
        /// Check if a field value differs between current and CSV data (for preview).
        /// </summary>
        private void CheckFieldChange(List<string> headers, List<string> fields, ImportOptions options,
            string currentValue, string fieldName, Dictionary<string, string> oldValues, Dictionary<string, string> newValues)
        {
            if (FindColumnIndex(headers, fieldName, options) < 0) return;

            var csvValue = GetFieldValue(headers, fields, fieldName, options);
            if (string.IsNullOrEmpty(csvValue)) return;

            string current = currentValue ?? "";
            string newVal = csvValue;

            if (!current.Equals(newVal, StringComparison.OrdinalIgnoreCase))
            {
                oldValues[fieldName] = current;
                newValues[fieldName] = newVal;
            }
        }

        /// <summary>
        /// Check if color values differ between current and CSV data (for preview).
        /// </summary>
        private void CheckColorChange(List<string> headers, List<string> fields, ImportOptions options,
            Color currentColor, Dictionary<string, string> oldValues, Dictionary<string, string> newValues)
        {
            if (FindColumnIndex(headers, "ColorR", options) < 0 ||
                FindColumnIndex(headers, "ColorG", options) < 0 ||
                FindColumnIndex(headers, "ColorB", options) < 0)
                return;

            var rStr = GetFieldValue(headers, fields, "ColorR", options);
            var gStr = GetFieldValue(headers, fields, "ColorG", options);
            var bStr = GetFieldValue(headers, fields, "ColorB", options);

            if (TryParseInt(rStr, out int r) && TryParseInt(gStr, out int g) && TryParseInt(bStr, out int b))
            {
                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));

                if (currentColor.R != r || currentColor.G != g || currentColor.B != b)
                {
                    oldValues["Color"] = $"RGB({currentColor.R},{currentColor.G},{currentColor.B})";
                    newValues["Color"] = $"RGB({r},{g},{b})";
                }
            }
        }
    }
}
