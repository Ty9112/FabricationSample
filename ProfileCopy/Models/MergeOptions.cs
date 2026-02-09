using System.Collections.Generic;

namespace FabricationSample.ProfileCopy.Models
{
    /// <summary>
    /// Options for a profile data copy operation.
    /// </summary>
    public class MergeOptions
    {
        /// <summary>
        /// Whether to create a backup of the target DATABASE folder before copying.
        /// Default is true.
        /// </summary>
        public bool CreateBackup { get; set; } = true;

        /// <summary>
        /// The data types selected for copying.
        /// </summary>
        public List<DataTypeDescriptor> SelectedDataTypes { get; set; } = new List<DataTypeDescriptor>();

        /// <summary>
        /// Always true for .map file copy - AutoCAD must be restarted for changes to take effect.
        /// </summary>
        public bool RequiresReload { get; } = true;
    }
}
