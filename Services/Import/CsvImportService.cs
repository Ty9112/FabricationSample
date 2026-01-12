using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FabricationSample.Utilities;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Base class for CSV import operations.
    /// Provides common functionality for all CSV-based imports.
    /// </summary>
    public abstract class CsvImportService : IImportService
    {
        /// <summary>
        /// Event raised to report progress during import.
        /// </summary>
        public event EventHandler<ImportProgressEventArgs> ProgressChanged;

        private bool _cancelled = false;

        /// <summary>
        /// Validate import file before processing.
        /// </summary>
        public ValidationResult Validate(string filePath, ImportOptions options = null)
        {
            try
            {
                options = options ?? new ImportOptions();

                // Basic file validation
                if (string.IsNullOrEmpty(filePath))
                    return ValidationResult.Invalid(new ValidationError(0, "File path cannot be empty"));

                if (!File.Exists(filePath))
                    return ValidationResult.Invalid(new ValidationError(0, $"File not found: {filePath}"));

                // Read and parse CSV
                var lines = ReadCsvFile(filePath, options);
                if (lines == null || lines.Count == 0)
                    return ValidationResult.Invalid(new ValidationError(0, "File is empty"));

                // Parse header
                var headerLine = lines[0];
                var headers = ParseCsvLine(headerLine, options.Delimiter);
                if (headers.Count == 0)
                    return ValidationResult.Invalid(new ValidationError(1, "Header row is empty or invalid"));

                // Validate columns (implemented by derived classes)
                var columnValidation = ValidateColumns(headers);
                if (!columnValidation.IsValid)
                    return columnValidation;

                // Validate data rows
                var result = new ValidationResult { IsValid = true };
                int startLine = options.HasHeaderRow ? 1 : 0;
                int dataRowCount = 0;

                for (int i = startLine; i < lines.Count; i++)
                {
                    int lineNumber = i + 1; // 1-based line numbers for user display
                    var line = lines[i];

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        result.Warnings.Add(new ValidationWarning(lineNumber, "Empty line skipped"));
                        continue;
                    }

                    var fields = ParseCsvLine(line, options.Delimiter);

                    // Check field count matches header
                    if (fields.Count != headers.Count)
                    {
                        result.Errors.Add(new ValidationError(lineNumber,
                            $"Field count mismatch. Expected {headers.Count}, found {fields.Count}"));
                        result.IsValid = false;
                        continue;
                    }

                    // Validate row data (implemented by derived classes)
                    var rowValidation = ValidateRow(lineNumber, headers, fields);
                    if (rowValidation.Errors.Count > 0)
                    {
                        result.Errors.AddRange(rowValidation.Errors);
                        result.IsValid = false;
                    }
                    if (rowValidation.Warnings.Count > 0)
                    {
                        result.Warnings.AddRange(rowValidation.Warnings);
                    }

                    dataRowCount++;
                }

                result.DataRowCount = dataRowCount;
                return result;
            }
            catch (Exception ex)
            {
                return ValidationResult.Invalid(new ValidationError(0, $"Validation error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Preview import changes without applying them.
        /// </summary>
        public ImportPreviewResult Preview(string filePath, ImportOptions options = null)
        {
            try
            {
                options = options ?? new ImportOptions();

                // First validate the file
                var validation = Validate(filePath, options);
                if (!validation.IsValid)
                    return ImportPreviewResult.Failure($"Validation failed: {validation.GetSummary()}");

                // Read and parse CSV
                var lines = ReadCsvFile(filePath, options);
                var headerLine = lines[0];
                var headers = ParseCsvLine(headerLine, options.Delimiter);

                // Generate preview (implemented by derived classes)
                var preview = GeneratePreview(headers, lines, options);
                return preview;
            }
            catch (Exception ex)
            {
                return ImportPreviewResult.Failure($"Preview error: {ex.Message}");
            }
        }

        /// <summary>
        /// Import data from CSV file.
        /// </summary>
        public ImportResult Import(string filePath, ImportOptions options = null)
        {
            try
            {
                _cancelled = false;
                options = options ?? new ImportOptions();

                ReportProgress(0, 100, "Starting import...", ImportPhase.Validating);

                // Validate unless explicitly skipped
                if (!options.SkipValidation)
                {
                    var validation = Validate(filePath, options);
                    if (!validation.IsValid)
                        return ImportResult.Failure($"Validation failed: {validation.GetSummary()}");
                }

                if (_cancelled)
                    return ImportResult.Cancelled();

                // Read and parse CSV
                ReportProgress(10, 100, "Reading file...", ImportPhase.Parsing);
                var lines = ReadCsvFile(filePath, options);
                var headerLine = lines[0];
                var headers = ParseCsvLine(headerLine, options.Delimiter);

                if (_cancelled)
                    return ImportResult.Cancelled();

                // Perform import (implemented by derived classes)
                ReportProgress(20, 100, "Importing data...", ImportPhase.Importing);
                var result = PerformImport(headers, lines, options);

                if (_cancelled)
                {
                    result.WasCancelled = true;
                    result.IsSuccess = false;
                    result.ErrorMessage = "Import was cancelled by user";
                }

                ReportProgress(100, 100, "Import complete", ImportPhase.Complete);
                return result;
            }
            catch (UnauthorizedAccessException ex)
            {
                return ImportResult.Failure($"Access denied: {ex.Message}");
            }
            catch (IOException ex)
            {
                return ImportResult.Failure($"File I/O error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ImportResult.Failure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancel ongoing import operation.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        /// <summary>
        /// Override to validate column headers specific to import type.
        /// </summary>
        /// <param name="headers">List of header column names</param>
        /// <returns>Validation result</returns>
        protected abstract ValidationResult ValidateColumns(List<string> headers);

        /// <summary>
        /// Override to validate a single data row.
        /// </summary>
        /// <param name="lineNumber">Line number in file (1-based)</param>
        /// <param name="headers">List of header column names</param>
        /// <param name="fields">List of field values for this row</param>
        /// <returns>Validation result</returns>
        protected abstract ValidationResult ValidateRow(int lineNumber, List<string> headers, List<string> fields);

        /// <summary>
        /// Override to generate preview of changes.
        /// </summary>
        /// <param name="headers">List of header column names</param>
        /// <param name="lines">All lines from CSV file</param>
        /// <param name="options">Import options</param>
        /// <returns>Preview result</returns>
        protected abstract ImportPreviewResult GeneratePreview(List<string> headers, List<string> lines, ImportOptions options);

        /// <summary>
        /// Override to perform the actual import operation.
        /// </summary>
        /// <param name="headers">List of header column names</param>
        /// <param name="lines">All lines from CSV file</param>
        /// <param name="options">Import options</param>
        /// <returns>Import result</returns>
        protected abstract ImportResult PerformImport(List<string> headers, List<string> lines, ImportOptions options);

        /// <summary>
        /// Read CSV file contents.
        /// </summary>
        protected List<string> ReadCsvFile(string filePath, ImportOptions options)
        {
            var encoding = Encoding.GetEncoding(options.Encoding);
            return File.ReadAllLines(filePath, encoding).ToList();
        }

        /// <summary>
        /// Parse a CSV line into fields, handling quotes and delimiters.
        /// </summary>
        protected List<string> ParseCsvLine(string line, char delimiter = ',')
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Check for escaped quote (two consecutive quotes)
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add final field
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// Report progress to listeners.
        /// </summary>
        protected void ReportProgress(int current, int total, string message, ImportPhase phase = ImportPhase.Importing)
        {
            if (_cancelled)
                return;

            int percentage = total > 0 ? (int)((current / (double)total) * 100) : 0;

            ProgressChanged?.Invoke(this, new ImportProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message,
                Percentage = percentage,
                Phase = phase
            });
        }

        /// <summary>
        /// Check if operation was cancelled.
        /// Derived classes should check this periodically during long operations.
        /// </summary>
        protected bool IsCancelled => _cancelled;

        /// <summary>
        /// Helper to find column index by name (case-insensitive).
        /// Returns -1 if not found.
        /// </summary>
        protected int FindColumnIndex(List<string> headers, string columnName)
        {
            return headers.FindIndex(h => h.Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper to check if required columns exist.
        /// </summary>
        protected ValidationResult ValidateRequiredColumns(List<string> headers, params string[] requiredColumns)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var required in requiredColumns)
            {
                if (FindColumnIndex(headers, required) == -1)
                {
                    result.Errors.Add(new ValidationError(1, $"Required column '{required}' not found in header"));
                    result.IsValid = false;
                }
            }

            return result;
        }

        /// <summary>
        /// Helper to get field value by column name.
        /// Returns empty string if column not found.
        /// </summary>
        protected string GetFieldValue(List<string> headers, List<string> fields, string columnName)
        {
            int index = FindColumnIndex(headers, columnName);
            if (index >= 0 && index < fields.Count)
                return fields[index];
            return string.Empty;
        }

        /// <summary>
        /// Helper to parse double value from field.
        /// </summary>
        protected bool TryParseDouble(string value, out double result)
        {
            // Handle empty values
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return false;
            }

            return double.TryParse(value, out result);
        }

        /// <summary>
        /// Helper to parse int value from field.
        /// </summary>
        protected bool TryParseInt(string value, out int result)
        {
            // Handle empty values
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0;
                return false;
            }

            return int.TryParse(value, out result);
        }
    }
}
