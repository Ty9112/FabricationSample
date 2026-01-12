using System;
using System.Collections.Generic;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Result of an export operation.
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Whether the export was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Path to exported file or folder.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Error message if export failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Whether the operation was cancelled by user.
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Number of rows/records exported.
        /// </summary>
        public int RowCount { get; set; }

        /// <summary>
        /// Number of records exported (alias for RowCount).
        /// </summary>
        public int RecordCount
        {
            get { return RowCount; }
            set { RowCount = value; }
        }

        /// <summary>
        /// Additional metadata about the export.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Create a successful export result.
        /// </summary>
        public static ExportResult Success(string filePath, int rowCount = 0)
        {
            return new ExportResult
            {
                IsSuccess = true,
                FilePath = filePath,
                RowCount = rowCount
            };
        }

        /// <summary>
        /// Create a failed export result.
        /// </summary>
        public static ExportResult Failure(string errorMessage)
        {
            return new ExportResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Create a cancelled export result.
        /// </summary>
        public static ExportResult Cancelled()
        {
            return new ExportResult
            {
                IsSuccess = false,
                WasCancelled = true,
                ErrorMessage = "Export was cancelled by user"
            };
        }
    }

    /// <summary>
    /// Configuration options for export operations.
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Whether to include a header row in CSV output.
        /// </summary>
        public bool IncludeHeader { get; set; } = true;

        /// <summary>
        /// Whether to exclude rows/fields with null or N/A values.
        /// </summary>
        public bool ExcludeNullValues { get; set; } = false;

        /// <summary>
        /// Whether to open the file/folder in Explorer after export.
        /// </summary>
        public bool OpenAfterExport { get; set; } = true;

        /// <summary>
        /// Timestamp format for generated filenames.
        /// Default: yyyyMMdd_HHmmss
        /// </summary>
        public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";

        /// <summary>
        /// Whether to quote all CSV fields (even if not required).
        /// </summary>
        public bool QuoteAllFields { get; set; } = false;

        /// <summary>
        /// Custom settings specific to particular export types.
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event arguments for progress reporting.
    /// </summary>
    public class ProgressEventArgs : EventArgs
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
        /// Optional additional data about progress.
        /// </summary>
        public object Data { get; set; }
    }
}
