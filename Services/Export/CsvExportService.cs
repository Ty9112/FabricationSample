using System;
using System.Collections.Generic;
using System.IO;
using FabricationSample.Utilities;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Base class for CSV export operations.
    /// Provides common functionality for all CSV-based exports.
    /// </summary>
    public abstract class CsvExportService : IExportService
    {
        /// <summary>
        /// Event raised to report progress during export.
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        private bool _cancelled = false;

        /// <summary>
        /// Export data to CSV file.
        /// </summary>
        /// <param name="outputPath">Full path to output CSV file</param>
        /// <param name="options">Export configuration options</param>
        /// <returns>Export result with success status and metadata</returns>
        public ExportResult Export(string outputPath, ExportOptions options = null)
        {
            try
            {
                _cancelled = false;
                options = options ?? new ExportOptions();

                // Validate output path
                if (string.IsNullOrEmpty(outputPath))
                    return ExportResult.Failure("Output path cannot be empty");

                // Create directory if needed
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        return ExportResult.Failure($"Failed to create directory: {ex.Message}");
                    }
                }

                // Generate CSV data (implemented by derived classes)
                ReportProgress(0, 100, "Starting export...");
                var csvData = GenerateCsvData(options);

                if (_cancelled)
                    return ExportResult.Cancelled();

                if (csvData == null || csvData.Count == 0)
                    return ExportResult.Failure("No data to export");

                // Write to file
                ReportProgress(90, 100, "Writing to file...");
                File.WriteAllLines(outputPath, csvData);

                if (_cancelled)
                {
                    // Try to clean up partial file
                    try { File.Delete(outputPath); } catch { }
                    return ExportResult.Cancelled();
                }

                ReportProgress(100, 100, "Export complete");

                // Calculate row count (excluding header if present)
                int rowCount = csvData.Count;
                if (options.IncludeHeader && rowCount > 0)
                    rowCount--;

                return ExportResult.Success(outputPath, rowCount);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ExportResult.Failure($"Access denied: {ex.Message}");
            }
            catch (IOException ex)
            {
                return ExportResult.Failure($"File I/O error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ExportResult.Failure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancel ongoing export operation.
        /// </summary>
        public void Cancel()
        {
            _cancelled = true;
        }

        /// <summary>
        /// Override to implement specific CSV data generation logic.
        /// </summary>
        /// <param name="options">Export configuration options</param>
        /// <returns>List of CSV lines (including header if specified)</returns>
        protected abstract List<string> GenerateCsvData(ExportOptions options);

        /// <summary>
        /// Report progress to listeners.
        /// </summary>
        /// <param name="current">Current progress value</param>
        /// <param name="total">Total progress value</param>
        /// <param name="message">Progress message</param>
        protected void ReportProgress(int current, int total, string message)
        {
            if (_cancelled)
                return;

            int percentage = total > 0 ? (int)((current / (double)total) * 100) : 0;

            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message,
                Percentage = percentage
            });
        }

        /// <summary>
        /// Check if operation was cancelled.
        /// Derived classes should check this periodically during long operations.
        /// </summary>
        protected bool IsCancelled => _cancelled;

        /// <summary>
        /// Helper method to create CSV header line from column names.
        /// </summary>
        /// <param name="columnNames">Array of column names</param>
        /// <returns>CSV-formatted header line</returns>
        protected string CreateHeaderLine(params string[] columnNames)
        {
            return CsvHelpers.WrapForCsv((object[])columnNames);
        }

        /// <summary>
        /// Helper method to create CSV data line from values.
        /// </summary>
        /// <param name="values">Array of values</param>
        /// <returns>CSV-formatted data line</returns>
        protected string CreateDataLine(params object[] values)
        {
            return CsvHelpers.WrapForCsv(values);
        }
    }
}
