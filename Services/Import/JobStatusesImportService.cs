using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Import service for Job Statuses.
    /// Updates existing job statuses from CSV format.
    /// Note: The Fabrication API does not support creating new job statuses,
    /// so this service can only update existing statuses.
    /// </summary>
    public class JobStatusesImportService : CsvImportService
    {
        /// <summary>
        /// Validate column headers.
        /// </summary>
        protected override ValidationResult ValidateColumns(List<string> headers)
        {
            // Description is required
            return ValidateRequiredColumns(headers, CurrentOptions, "Description");
        }

        /// <summary>
        /// Validate a single row of data.
        /// </summary>
        protected override ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields)
        {
            var result = new ValidationResult { IsValid = true };

            string description = GetFieldValue(headers, fields, "Description", CurrentOptions);
            if (string.IsNullOrWhiteSpace(description))
            {
                result.Errors.Add(new ValidationError(lineNumber, "Description is required"));
                result.IsValid = false;
            }

            // Validate boolean fields if provided
            ValidateBoolField(result, lineNumber, headers, fields, "Active");
            ValidateBoolField(result, lineNumber, headers, fields, "DoSave");
            ValidateBoolField(result, lineNumber, headers, fields, "DoExport");
            ValidateBoolField(result, lineNumber, headers, fields, "DeActivateOnCompletion");

            // Validate DoCopy if provided
            string doCopyStr = GetFieldValue(headers, fields, "DoCopy", CurrentOptions);
            if (!string.IsNullOrWhiteSpace(doCopyStr))
            {
                if (!Enum.TryParse<JobStatusAction>(doCopyStr, true, out _))
                {
                    result.Warnings.Add(new ValidationWarning(lineNumber,
                        $"Invalid DoCopy value '{doCopyStr}', must be Nothing, Copy, or Move"));
                }
            }

            return result;
        }

        private void ValidateBoolField(ValidationResult result, int lineNumber, List<string> headers, List<string> fields, string fieldName)
        {
            string value = GetFieldValue(headers, fields, fieldName, CurrentOptions);
            if (!string.IsNullOrWhiteSpace(value) && !bool.TryParse(value, out _))
            {
                result.Warnings.Add(new ValidationWarning(lineNumber,
                    $"Invalid {fieldName} value '{value}', must be True or False"));
            }
        }

        /// <summary>
        /// Generate preview of import changes.
        /// </summary>
        protected override ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options)
        {
            var preview = new ImportPreviewResult { IsSuccess = true };
            int startLine = options.HasHeaderRow ? 1 : 0;

            var existingStatuses = Database.JobStatuses.ToDictionary(
                s => s.Description?.ToLowerInvariant() ?? "", s => s);

            for (int i = startLine; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line, options.Delimiter);
                string description = GetFieldValue(headers, fields, "Description", options);

                if (string.IsNullOrWhiteSpace(description))
                    continue;

                if (existingStatuses.ContainsKey(description.ToLowerInvariant()))
                {
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Update",
                        Description = $"Update job status '{description}'"
                    });
                    preview.UpdatedRecordCount++;
                }
                else
                {
                    // Cannot create new job statuses via API
                    preview.Changes.Add(new PreviewChange
                    {
                        LineNumber = i + 1,
                        ChangeType = "Skip",
                        Description = $"Skip - job status '{description}' not found (API does not support creation)"
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

            var existingStatuses = Database.JobStatuses.ToDictionary(
                s => s.Description?.ToLowerInvariant() ?? "", s => s);

            for (int i = startLine; i < lines.Count; i++)
            {
                if (IsCancelled)
                    break;

                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                current++;
                ReportProgress(current, total, $"Importing job status {current} of {total}", ImportPhase.Importing);

                var fields = ParseCsvLine(line, options.Delimiter);
                string description = GetFieldValue(headers, fields, "Description", options);

                if (string.IsNullOrWhiteSpace(description))
                {
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    JobStatus status;
                    string descKey = description.ToLowerInvariant();

                    if (existingStatuses.TryGetValue(descKey, out status))
                    {
                        // Update existing status
                        UpdateJobStatus(status, headers, fields, options);
                        result.ImportedCount++;
                    }
                    else
                    {
                        // Cannot create new job statuses via API
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors[i + 1] = $"Error processing status '{description}': {ex.Message}";

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
        /// Update job status properties from CSV fields.
        /// </summary>
        private void UpdateJobStatus(JobStatus status, List<string> headers, List<string> fields, ImportOptions options)
        {
            // Active
            string activeStr = GetFieldValue(headers, fields, "Active", options);
            if (!string.IsNullOrWhiteSpace(activeStr) && bool.TryParse(activeStr, out bool active))
            {
                status.Active = active;
            }

            // DoCopy
            string doCopyStr = GetFieldValue(headers, fields, "DoCopy", options);
            if (!string.IsNullOrWhiteSpace(doCopyStr) && Enum.TryParse<JobStatusAction>(doCopyStr, true, out var doCopy))
            {
                status.DoCopy = doCopy;
            }

            // CopyJobToFolder
            string copyJobToFolder = GetFieldValue(headers, fields, "CopyJobToFolder", options);
            if (!string.IsNullOrEmpty(copyJobToFolder))
            {
                status.CopyJobToFolder = copyJobToFolder;
            }

            // DoSave
            string doSaveStr = GetFieldValue(headers, fields, "DoSave", options);
            if (!string.IsNullOrWhiteSpace(doSaveStr) && bool.TryParse(doSaveStr, out bool doSave))
            {
                status.DoSave = doSave;
            }

            // DoExport
            string doExportStr = GetFieldValue(headers, fields, "DoExport", options);
            if (!string.IsNullOrWhiteSpace(doExportStr) && bool.TryParse(doExportStr, out bool doExport))
            {
                status.DoExport = doExport;
            }

            // ExportFile
            string exportFile = GetFieldValue(headers, fields, "ExportFile", options);
            if (!string.IsNullOrEmpty(exportFile))
            {
                status.ExportFile = exportFile;
            }

            // DeActivateOnCompletion
            string deactivateStr = GetFieldValue(headers, fields, "DeActivateOnCompletion", options);
            if (!string.IsNullOrWhiteSpace(deactivateStr) && bool.TryParse(deactivateStr, out bool deactivate))
            {
                status.DeActivateOnCompletion = deactivate;
            }
        }
    }
}
