using System.IO;

namespace FabricationSample.ProfileCopy.Models
{
    /// <summary>
    /// Represents a discovered Fabrication profile on the filesystem.
    /// </summary>
    public class ProfileInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string DatabasePath { get; set; }
        public string Version { get; set; }
        public bool IsCurrent { get; set; }

        /// <summary>
        /// Checks whether this profile has a valid DATABASE folder.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(DatabasePath) && Directory.Exists(DatabasePath);
        }

        public override string ToString()
        {
            string suffix = IsCurrent ? " (Current)" : "";
            return $"{Name}{suffix} [{Version}]";
        }
    }
}
