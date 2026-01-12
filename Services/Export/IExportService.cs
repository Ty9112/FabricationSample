using System;

namespace FabricationSample.Services.Export
{
    /// <summary>
    /// Base interface for all export services.
    /// Defines common contract for export operations.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Export data to specified output path.
        /// </summary>
        /// <param name="outputPath">Full path to output file or folder</param>
        /// <param name="options">Optional export configuration</param>
        /// <returns>Export result with success status and metadata</returns>
        ExportResult Export(string outputPath, ExportOptions options = null);

        /// <summary>
        /// Cancel an ongoing export operation.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Event raised to report progress during export.
        /// </summary>
        event EventHandler<ProgressEventArgs> ProgressChanged;
    }
}
