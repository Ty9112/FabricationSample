using System;
using System.Collections.Generic;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Result of an import operation.
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// Whether the import was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Path to imported file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Error message if import failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether the operation was cancelled by user.
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Number of records successfully imported.
        /// </summary>
        public int ImportedCount { get; set; }

        /// <summary>
        /// Number of records skipped (duplicates, invalid data, etc.).
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Number of records that failed to import.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Detailed error messages for failed records.
        /// Key is line number, value is error message.
        /// </summary>
        public Dictionary<int, string> Errors { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// Additional metadata about the import.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Create a successful import result.
        /// </summary>
        public static ImportResult Success(string filePath, int importedCount, int skippedCount = 0)
        {
            return new ImportResult
            {
                IsSuccess = true,
                FilePath = filePath,
                ImportedCount = importedCount,
                SkippedCount = skippedCount
            };
        }

        /// <summary>
        /// Create a failed import result.
        /// </summary>
        public static ImportResult Failure(string errorMessage)
        {
            return new ImportResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Create a cancelled import result.
        /// </summary>
        public static ImportResult Cancelled()
        {
            return new ImportResult
            {
                IsSuccess = false,
                WasCancelled = true,
                ErrorMessage = "Import was cancelled by user"
            };
        }

        /// <summary>
        /// Get a summary message describing the import result.
        /// </summary>
        public string GetSummary()
        {
            if (WasCancelled)
                return "Import was cancelled.";

            if (!IsSuccess)
                return $"Import failed: {ErrorMessage}";

            var summary = $"Import completed successfully.\n";
            summary += $"Imported: {ImportedCount} records\n";
            if (SkippedCount > 0)
                summary += $"Skipped: {SkippedCount} records\n";
            if (ErrorCount > 0)
                summary += $"Errors: {ErrorCount} records\n";

            return summary;
        }
    }

    /// <summary>
    /// Result of import validation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether the validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors.
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        /// <summary>
        /// List of validation warnings (non-blocking).
        /// </summary>
        public List<ValidationWarning> Warnings { get; set; } = new List<ValidationWarning>();

        /// <summary>
        /// Number of data rows found (excluding header).
        /// </summary>
        public int DataRowCount { get; set; }

        /// <summary>
        /// Create a successful validation result.
        /// </summary>
        public static ValidationResult Valid(int dataRowCount)
        {
            return new ValidationResult
            {
                IsValid = true,
                DataRowCount = dataRowCount
            };
        }

        /// <summary>
        /// Create a failed validation result.
        /// </summary>
        public static ValidationResult Invalid(params ValidationError[] errors)
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>(errors)
            };
        }

        /// <summary>
        /// Get a summary of validation errors and warnings.
        /// </summary>
        public string GetSummary()
        {
            if (IsValid && Warnings.Count == 0)
                return $"Validation passed. Found {DataRowCount} data rows.";

            var summary = "";
            if (!IsValid)
            {
                summary += $"Validation failed with {Errors.Count} error(s):\n";
                foreach (var error in Errors)
                    summary += $"  - {error}\n";
            }

            if (Warnings.Count > 0)
            {
                summary += $"\n{Warnings.Count} warning(s):\n";
                foreach (var warning in Warnings)
                    summary += $"  - {warning}\n";
            }

            return summary;
        }
    }

    /// <summary>
    /// Validation error with line number and message.
    /// </summary>
    public class ValidationError
    {
        public int LineNumber { get; set; }
        public string Message { get; set; }

        public ValidationError(int lineNumber, string message)
        {
            LineNumber = lineNumber;
            Message = message;
        }

        public override string ToString()
        {
            return $"Line {LineNumber}: {Message}";
        }
    }

    /// <summary>
    /// Validation warning with line number and message.
    /// </summary>
    public class ValidationWarning
    {
        public int LineNumber { get; set; }
        public string Message { get; set; }

        public ValidationWarning(int lineNumber, string message)
        {
            LineNumber = lineNumber;
            Message = message;
        }

        public override string ToString()
        {
            return $"Line {LineNumber}: {Message}";
        }
    }

    /// <summary>
    /// Preview of import changes without applying them.
    /// </summary>
    public class ImportPreviewResult
    {
        /// <summary>
        /// Whether the preview was generated successfully.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if preview failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// List of changes that would be made.
        /// </summary>
        public List<PreviewChange> Changes { get; set; } = new List<PreviewChange>();

        /// <summary>
        /// Number of new records that would be created.
        /// </summary>
        public int NewRecordCount { get; set; }

        /// <summary>
        /// Number of existing records that would be updated.
        /// </summary>
        public int UpdatedRecordCount { get; set; }

        /// <summary>
        /// Number of records that would be skipped.
        /// </summary>
        public int SkippedRecordCount { get; set; }

        /// <summary>
        /// Create a successful preview result.
        /// </summary>
        public static ImportPreviewResult Success()
        {
            return new ImportPreviewResult { IsSuccess = true };
        }

        /// <summary>
        /// Create a failed preview result.
        /// </summary>
        public static ImportPreviewResult Failure(string errorMessage)
        {
            return new ImportPreviewResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Get a summary of preview changes.
        /// </summary>
        public string GetSummary()
        {
            if (!IsSuccess)
                return $"Preview failed: {ErrorMessage}";

            var summary = "Preview of changes:\n";
            summary += $"  New records: {NewRecordCount}\n";
            summary += $"  Updated records: {UpdatedRecordCount}\n";
            summary += $"  Skipped records: {SkippedRecordCount}\n";
            summary += $"  Total changes: {Changes.Count}";

            return summary;
        }
    }

    /// <summary>
    /// Represents a single change in the import preview.
    /// </summary>
    public class PreviewChange
    {
        public int LineNumber { get; set; }
        public string ChangeType { get; set; } // "New", "Update", "Skip"
        public string Description { get; set; }
        public Dictionary<string, string> OldValues { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> NewValues { get; set; } = new Dictionary<string, string>();

        public override string ToString()
        {
            return $"Line {LineNumber} - {ChangeType}: {Description}";
        }
    }

    /// <summary>
    /// Configuration options for import operations.
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// Whether to skip the first row (header row).
        /// </summary>
        public bool HasHeaderRow { get; set; } = true;

        /// <summary>
        /// Whether to update existing records or skip them.
        /// </summary>
        public bool UpdateExisting { get; set; } = false;

        /// <summary>
        /// Whether to skip validation and force import.
        /// Use with caution - may result in data corruption.
        /// </summary>
        public bool SkipValidation { get; set; } = false;

        /// <summary>
        /// Whether to stop on first error or continue.
        /// </summary>
        public bool StopOnFirstError { get; set; } = false;

        /// <summary>
        /// CSV delimiter character (default: comma).
        /// </summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>
        /// Character encoding for file reading.
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";

        /// <summary>
        /// Custom settings specific to particular import types.
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event arguments for import progress reporting.
    /// </summary>
    public class ImportProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Current progress value (e.g., number of items processed).
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Total progress value (e.g., total number of items).
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Progress message describing current operation.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Current phase of import operation.
        /// </summary>
        public ImportPhase Phase { get; set; }

        /// <summary>
        /// Optional additional data about progress.
        /// </summary>
        public object Data { get; set; }
    }

    /// <summary>
    /// Import operation phases.
    /// </summary>
    public enum ImportPhase
    {
        Validating,
        Parsing,
        Importing,
        Complete
    }
}
