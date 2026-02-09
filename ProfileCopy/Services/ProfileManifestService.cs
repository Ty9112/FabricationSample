using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using FabricationSample.ProfileCopy.Models;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.ProfileCopy.Services
{
    public class ProfileManifestService
    {
        private const string ManifestFileName = ".fabmanifest.json";

        /// <summary>
        /// Generates a manifest of all enumerable items in the currently loaded profile
        /// and saves it as JSON in the profile's DATABASE folder.
        /// </summary>
        public ProfileManifest GenerateManifest(string databasePath, string profileName)
        {
            var manifest = new ProfileManifest
            {
                ProfileName = profileName ?? "Global",
                DatabasePath = databasePath,
                GeneratedAt = DateTime.Now
            };

            // Services
            try
            {
                var items = new List<ManifestItem>();
                foreach (var svc in FabDB.Services)
                    items.Add(new ManifestItem { Name = svc.Name, Group = svc.Group });
                if (items.Count > 0)
                    manifest.DataTypes["Services"] = items;
            }
            catch { }

            // Materials
            try
            {
                var items = new List<ManifestItem>();
                foreach (var mat in FabDB.Materials)
                    items.Add(new ManifestItem { Name = mat.Name, Group = mat.Group });
                if (items.Count > 0)
                    manifest.DataTypes["Materials"] = items;
            }
            catch { }

            // Specifications
            try
            {
                var items = new List<ManifestItem>();
                foreach (var spec in FabDB.Specifications)
                    items.Add(new ManifestItem { Name = spec.Name, Group = spec.Group });
                if (items.Count > 0)
                    manifest.DataTypes["Specifications"] = items;
            }
            catch { }

            // Sections
            try
            {
                var items = new List<ManifestItem>();
                foreach (var sec in FabDB.Sections)
                    items.Add(new ManifestItem { Name = sec.Description, Group = sec.Group });
                if (items.Count > 0)
                    manifest.DataTypes["Sections"] = items;
            }
            catch { }

            // Connectors
            try
            {
                var items = new List<ManifestItem>();
                foreach (var conn in FabDB.Connectors)
                    items.Add(new ManifestItem { Name = conn.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Connectors"] = items;
            }
            catch { }

            // Seams
            try
            {
                var items = new List<ManifestItem>();
                foreach (var seam in FabDB.Seams)
                    items.Add(new ManifestItem { Name = seam.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Seams"] = items;
            }
            catch { }

            // Dampers
            try
            {
                var items = new List<ManifestItem>();
                foreach (var d in FabDB.Dampers)
                    items.Add(new ManifestItem { Name = d.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Dampers"] = items;
            }
            catch { }

            // Splitters
            try
            {
                var items = new List<ManifestItem>();
                foreach (var s in FabDB.Splitters)
                    items.Add(new ManifestItem { Name = s.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Splitters"] = items;
            }
            catch { }

            // Stiffeners
            try
            {
                var items = new List<ManifestItem>();
                foreach (var s in FabDB.Stiffeners)
                    items.Add(new ManifestItem { Name = s.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Stiffeners"] = items;
            }
            catch { }

            // Ancillaries
            try
            {
                var items = new List<ManifestItem>();
                foreach (var a in FabDB.Ancillaries)
                    items.Add(new ManifestItem { Name = a.Description, Group = a.AncillaryType.ToString() });
                if (items.Count > 0)
                    manifest.DataTypes["Ancillaries"] = items;
            }
            catch { }

            // Suppliers
            try
            {
                var items = new List<ManifestItem>();
                foreach (var sg in FabDB.SupplierGroups)
                    items.Add(new ManifestItem { Name = sg.Name });
                if (items.Count > 0)
                    manifest.DataTypes["Suppliers"] = items;
            }
            catch { }

            // Costs (PriceLists within SupplierGroups)
            try
            {
                var items = new List<ManifestItem>();
                foreach (var sg in FabDB.SupplierGroups)
                {
                    foreach (var pl in sg.PriceLists)
                        items.Add(new ManifestItem { Name = pl.Name, Group = sg.Name });
                }
                if (items.Count > 0)
                    manifest.DataTypes["Costs"] = items;
            }
            catch { }

            // Installation Times
            try
            {
                var items = new List<ManifestItem>();
                foreach (var t in FabDB.InstallationTimesTable)
                    items.Add(new ManifestItem { Name = t.Name, Group = t.Group });
                if (items.Count > 0)
                    manifest.DataTypes["InstallationTimes"] = items;
            }
            catch { }

            // Fabrication Times
            try
            {
                var items = new List<ManifestItem>();
                foreach (var t in FabDB.FabricationTimesTable)
                    items.Add(new ManifestItem { Name = t.Name, Group = t.Group });
                if (items.Count > 0)
                    manifest.DataTypes["FabricationTimes"] = items;
            }
            catch { }

            // Save to disk
            SaveManifest(manifest, databasePath);
            return manifest;
        }

        /// <summary>
        /// Loads a cached manifest from the given DATABASE folder. Returns null if not found.
        /// </summary>
        public ProfileManifest LoadManifest(string databasePath)
        {
            string path = GetManifestPath(databasePath);
            if (!File.Exists(path))
                return null;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(ProfileManifest));
                using (var stream = File.OpenRead(path))
                {
                    return serializer.ReadObject(stream) as ProfileManifest;
                }
            }
            catch
            {
                return null;
            }
        }

        public string GetManifestPath(string databasePath)
        {
            return Path.Combine(databasePath, ManifestFileName);
        }

        public bool HasManifest(string databasePath)
        {
            return File.Exists(GetManifestPath(databasePath));
        }

        private void SaveManifest(ProfileManifest manifest, string databasePath)
        {
            string path = GetManifestPath(databasePath);
            var serializer = new DataContractJsonSerializer(typeof(ProfileManifest));
            using (var stream = File.Create(path))
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true))
                {
                    serializer.WriteObject(writer, manifest);
                }
            }
        }
    }
}
