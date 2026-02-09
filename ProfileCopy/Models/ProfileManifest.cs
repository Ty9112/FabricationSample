using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FabricationSample.ProfileCopy.Models
{
    [DataContract]
    public class ProfileManifest
    {
        [DataMember(Name = "profileName")]
        public string ProfileName { get; set; }

        [DataMember(Name = "databasePath")]
        public string DatabasePath { get; set; }

        [DataMember(Name = "generatedAt")]
        public DateTime GeneratedAt { get; set; }

        [DataMember(Name = "dataTypes")]
        public Dictionary<string, List<ManifestItem>> DataTypes { get; set; }

        public ProfileManifest()
        {
            DataTypes = new Dictionary<string, List<ManifestItem>>();
        }
    }

    [DataContract]
    public class ManifestItem
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "group")]
        public string Group { get; set; }
    }

    [DataContract]
    public class PendingCleanup
    {
        [DataMember(Name = "profileName")]
        public string ProfileName { get; set; }

        [DataMember(Name = "databasePath")]
        public string DatabasePath { get; set; }

        [DataMember(Name = "createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// DataType key (e.g. "Services") to list of item NAMES to DELETE.
        /// </summary>
        [DataMember(Name = "itemsToDelete")]
        public Dictionary<string, List<string>> ItemsToDelete { get; set; }

        public PendingCleanup()
        {
            ItemsToDelete = new Dictionary<string, List<string>>();
        }
    }
}
