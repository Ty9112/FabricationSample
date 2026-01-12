using System;

namespace FabricationSample.Services.Import
{
    /// <summary>
    /// Base interface for all import services.
    /// Defines common contract for import operations.
    /// </summary>
    public interface IImportService
    {
        /// <summary>
        /// Validate import file before processing.
        /// </summary>
        /// <param name="filePath">Full path to import file</param>
        /// <param name="options">Optional import configuration</param>
        /// <returns>Validation result with detailed errors if validation fails</returns>
        ValidationResult Validate(string filePath, ImportOptions options = null);

        /// <summary>
        /// Preview import changes without applying them (dry-run).
        /// </summary>
        /// <param name="filePath">Full path to import file</param>
        /// <param name="options">Optional import configuration</param>
        /// <returns>Preview result showing what would be imported</returns>
        ImportPreviewResult Preview(string filePath, ImportOptions options = null);

        /// <summary>
        /// Import data from specified file.
        /// </summary>
        /// <param name="filePath">Full path to import file</param>
        /// <param name="options">Optional import configuration</param>
        /// <returns>Import result with success status and metadata</returns>
        ImportResult Import(string filePath, ImportOptions options = null);

        /// <summary>
        /// Cancel an ongoing import operation.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Event raised to report progress during import.
        /// </summary>
        event EventHandler<ImportProgressEventArgs> ProgressChanged;
    }
}
