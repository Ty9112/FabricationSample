using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for service template data (button codes and item assignments).
    /// Matches the CSV format produced by ServiceTemplateDataExportService.
    ///
    /// CSV Format:
    /// - Required columns: Service Name (or Template Name), Tab, Name (button name)
    /// - Optional columns: Button Code, Item Path1-4, Condition1-4
    /// </summary>
    public class ServiceTemplateDataImportService : CsvImportService
    {
        /// <summary>
        /// Whether to match by template name instead of service name.
        /// </summary>
        public bool MatchByTemplate { get; set; }

        /// <summary>
        /// Validate column headers for service template data import.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            // Need either "Service Name" or "Template Name" plus Tab and Name
            bool hasServiceName = FindColumnIndex(headers, "Service Name", CurrentOptions) >= 0;
            bool hasTemplateName = FindColumnIndex(headers, "Template Name", CurrentOptions) >= 0;

            if (!hasServiceName && !hasTemplateName)
            {
                return ValidationResult.Invalid(new ValidationError(1,
                    "Required column 'Service Name' or 'Template Name' not found in header"));
            }

            // If we have Template Name but not Service Name, we match by template
            if (!hasServiceName && hasTemplateName)
                MatchByTemplate = true;

            var result = ValidateRequiredColumns(headers, CurrentOptions, "Tab", "Name");
            return result;
        }

        /// <summary>
        /// Validate a single data row.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            // Name (button name) can be empty for tab header rows
            var name = GetFieldValue(headers, fields, "Name", CurrentOptions);
            var tab = GetFieldValue(headers, fields, "Tab", CurrentOptions);

            if (string.IsNullOrWhiteSpace(tab))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber, "Tab name is empty, row will be skipped"));
            }

            return result;
        }

        /// <summary>
        /// Generate preview of service template data import changes.
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

                var buttonName = GetFieldValue(headers, fields, "Name", options);
                var tabName = GetFieldValue(headers, fields, "Tab", options);
                var buttonCode = GetFieldValue(headers, fields, "Button Code", options);

                // Skip rows without a button name (tab header rows)
                if (string.IsNullOrWhiteSpace(buttonName))
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Tab header row for '{tabName}' (no button name)"
                    });
                    continue;
                }

                // Find the matching service/template and button
                ServiceButton matchedButton = null;
                string matchContext = "";

                if (MatchByTemplate)
                {
                    var templateName = GetFieldValue(headers, fields, "Template Name", options);
                    matchedButton = FindButtonByTemplate(templateName, tabName, buttonName);
                    matchContext = $"Template '{templateName}'";
                }
                else
                {
                    var serviceName = GetFieldValue(headers, fields, "Service Name", options);
                    matchedButton = FindButtonByService(serviceName, tabName, buttonName);
                    matchContext = $"Service '{serviceName}'";
                }

                if (matchedButton == null)
                {
                    preview.SkippedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Skip",
                        Description = $"Button '{buttonName}' not found in {matchContext} / Tab '{tabName}'"
                    });
                    continue;
                }

                // Check if there's anything to update
                bool hasChanges = false;
                var oldValues = new Dictionary<string, string>();
                var newValues = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(buttonCode))
                {
                    string currentCode = matchedButton.ButtonCode ?? "";
                    if (!currentCode.Equals(buttonCode))
                    {
                        oldValues["Button Code"] = currentCode;
                        newValues["Button Code"] = buttonCode;
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    preview.UpdatedRecordCount++;
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = lineNumber,
                        ChangeType = "Update",
                        Description = $"Update button '{buttonName}' in {matchContext}",
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
                        Description = $"No changes for button '{buttonName}' in {matchContext}"
                    });
                }
            }

            return preview;
        }

        /// <summary>
        /// Perform the service template data import operation.
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
                    var buttonName = GetFieldValue(headers, fields, "Name", options);
                    var tabName = GetFieldValue(headers, fields, "Tab", options);
                    var buttonCode = GetFieldValue(headers, fields, "Button Code", options);

                    // Skip tab header rows
                    if (string.IsNullOrWhiteSpace(buttonName))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Find matching button
                    ServiceButton matchedButton = null;

                    if (MatchByTemplate)
                    {
                        var templateName = GetFieldValue(headers, fields, "Template Name", options);
                        matchedButton = FindButtonByTemplate(templateName, tabName, buttonName);
                    }
                    else
                    {
                        var serviceName = GetFieldValue(headers, fields, "Service Name", options);
                        matchedButton = FindButtonByService(serviceName, tabName, buttonName);
                    }

                    if (matchedButton == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Update button code
                    bool updated = false;
                    if (!string.IsNullOrEmpty(buttonCode))
                    {
                        string currentCode = matchedButton.ButtonCode ?? "";
                        if (!currentCode.Equals(buttonCode))
                        {
                            matchedButton.ButtonCode = buttonCode;
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

        /// <summary>
        /// Find a button by service name, tab name, and button name.
        /// </summary>
        private ServiceButton FindButtonByService(string serviceName, string tabName, string buttonName)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return null;

            var service = FabDB.Services.FirstOrDefault(s =>
                s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

            if (service?.ServiceTemplate?.ServiceTabs == null) return null;

            return FindButtonInTabs(service.ServiceTemplate.ServiceTabs, tabName, buttonName);
        }

        /// <summary>
        /// Find a button by template name, tab name, and button name.
        /// </summary>
        private ServiceButton FindButtonByTemplate(string templateName, string tabName, string buttonName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return null;

            // Search through all services' templates for a matching template name
            foreach (var service in FabDB.Services)
            {
                if (service.ServiceTemplate == null) continue;
                if (!service.ServiceTemplate.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase)) continue;

                if (service.ServiceTemplate.ServiceTabs == null) continue;

                var button = FindButtonInTabs(service.ServiceTemplate.ServiceTabs, tabName, buttonName);
                if (button != null) return button;
            }

            return null;
        }

        /// <summary>
        /// Find a button within a collection of tabs.
        /// </summary>
        private ServiceButton FindButtonInTabs(IEnumerable<ServiceTab> tabs, string tabName, string buttonName)
        {
            foreach (var tab in tabs)
            {
                if (tab.ServiceButtons == null) continue;

                // Match tab by name
                bool tabMatches = tab.Name != null &&
                    tab.Name.Equals(tabName, StringComparison.OrdinalIgnoreCase);

                if (!tabMatches) continue;

                foreach (var button in tab.ServiceButtons)
                {
                    if (button.Name != null &&
                        button.Name.Equals(buttonName, StringComparison.OrdinalIgnoreCase))
                    {
                        return button;
                    }
                }
            }

            return null;
        }
    }
}
