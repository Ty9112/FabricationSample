using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Utilities;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.ProfileCopy.Services
{
    public class SelectiveCleanupService
    {
        private const string CleanupFileName = "_pending_cleanup.json";

        // --- Legacy path (backup directory) ---

        public string GetCleanupFilePath()
        {
            return Path.Combine(ProfilePathHelper.GetBackupDirectory(), CleanupFileName);
        }

        public bool HasPendingCleanup()
        {
            return File.Exists(GetCleanupFilePath());
        }

        public void SavePendingCleanup(PendingCleanup cleanup)
        {
            string dir = ProfilePathHelper.GetBackupDirectory();
            ProfilePathHelper.EnsureDirectoryExists(dir);

            string path = GetCleanupFilePath();
            WriteCleanupFile(path, cleanup);
        }

        public PendingCleanup LoadPendingCleanup()
        {
            return ReadCleanupFile(GetCleanupFilePath());
        }

        public string ExecutePendingCleanup()
        {
            return ExecuteCleanupFromFile(GetCleanupFilePath());
        }

        // --- Per-profile path (target profile's DATABASE folder) ---

        public string GetCleanupFilePath(string databasePath)
        {
            return Path.Combine(databasePath, CleanupFileName);
        }

        public bool HasPendingCleanup(string databasePath)
        {
            return File.Exists(GetCleanupFilePath(databasePath));
        }

        public void SavePendingCleanup(PendingCleanup cleanup, string databasePath)
        {
            string path = GetCleanupFilePath(databasePath);
            WriteCleanupFile(path, cleanup);
        }

        public PendingCleanup LoadPendingCleanup(string databasePath)
        {
            return ReadCleanupFile(GetCleanupFilePath(databasePath));
        }

        public string ExecutePendingCleanup(string databasePath)
        {
            return ExecuteCleanupFromFile(GetCleanupFilePath(databasePath));
        }

        // --- Shared implementation ---

        private void WriteCleanupFile(string path, PendingCleanup cleanup)
        {
            var serializer = new DataContractJsonSerializer(typeof(PendingCleanup));
            using (var stream = File.Create(path))
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true))
                {
                    serializer.WriteObject(writer, cleanup);
                }
            }
        }

        private PendingCleanup ReadCleanupFile(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(PendingCleanup));
                using (var stream = File.OpenRead(path))
                {
                    return serializer.ReadObject(stream) as PendingCleanup;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Executes pending cleanup by deleting unwanted items via the Fabrication API,
        /// then removing the cleanup file. Returns a summary of actions taken.
        /// </summary>
        private string ExecuteCleanupFromFile(string path)
        {
            var cleanup = ReadCleanupFile(path);
            if (cleanup == null)
                return null;

            var results = new List<string>();
            int totalDeleted = 0;

            foreach (var kvp in cleanup.ItemsToDelete)
            {
                string dataType = kvp.Key;
                var namesToDelete = kvp.Value;
                if (namesToDelete == null || namesToDelete.Count == 0)
                    continue;

                try
                {
                    int deleted = DeleteItems(dataType, namesToDelete);
                    totalDeleted += deleted;
                    results.Add($"{dataType}: deleted {deleted}/{namesToDelete.Count}");
                }
                catch (Exception ex)
                {
                    results.Add($"{dataType}: ERROR - {ex.Message}");
                }
            }

            // Remove cleanup file
            try
            {
                File.Delete(path);
            }
            catch { }

            string summary = $"Selective cleanup complete: {totalDeleted} item(s) deleted.";
            if (results.Count > 0)
                summary += "\n\n" + string.Join("\n", results);

            return summary;
        }

        private int DeleteItems(string dataType, List<string> namesToDelete)
        {
            var nameSet = new HashSet<string>(namesToDelete, StringComparer.OrdinalIgnoreCase);
            int deleted = 0;

            switch (dataType)
            {
                case "Services":
                    var servicesToDelete = FabDB.Services.Where(s => nameSet.Contains(s.Name)).ToList();
                    foreach (var svc in servicesToDelete)
                    {
                        FabDB.DeleteService(svc);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveServices();
                    break;

                case "Materials":
                    var materialsToDelete = FabDB.Materials.Where(m => nameSet.Contains(m.Name)).ToList();
                    foreach (var mat in materialsToDelete)
                    {
                        FabDB.DeleteMaterial(mat);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveMaterials();
                    break;

                case "Specifications":
                    var specsToDelete = FabDB.Specifications.Where(s => nameSet.Contains(s.Name)).ToList();
                    foreach (var spec in specsToDelete)
                    {
                        FabDB.DeleteSpecification(spec);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveSpecifications();
                    break;

                case "Sections":
                    var sectionsToDelete = FabDB.Sections.Where(s => nameSet.Contains(s.Description)).ToList();
                    foreach (var sec in sectionsToDelete)
                    {
                        FabDB.DeleteSection(sec);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveSections();
                    break;

                case "Connectors":
                    var connectorsToDelete = FabDB.Connectors.Where(c => nameSet.Contains(c.Name)).ToList();
                    foreach (var conn in connectorsToDelete)
                    {
                        FabDB.DeleteConnector(conn);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveConnectors();
                    break;

                case "Dampers":
                    var dampersToDelete = FabDB.Dampers.Where(d => nameSet.Contains(d.Name)).ToList();
                    foreach (var d in dampersToDelete)
                    {
                        FabDB.DeleteDamper(d);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveDampers();
                    break;

                case "Stiffeners":
                    var stiffenersToDelete = FabDB.Stiffeners.Where(s => nameSet.Contains(s.Name)).ToList();
                    foreach (var s in stiffenersToDelete)
                    {
                        FabDB.DeleteStiffener(s);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveStiffeners();
                    break;

                case "Ancillaries":
                    var ancillariesToDelete = FabDB.Ancillaries.Where(a => nameSet.Contains(a.Description)).ToList();
                    foreach (var a in ancillariesToDelete)
                    {
                        FabDB.DeleteAncillary(a);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveAncillaries();
                    break;

                case "Suppliers":
                    var suppliersToDelete = FabDB.SupplierGroups.Where(sg => nameSet.Contains(sg.Name)).ToList();
                    foreach (var sg in suppliersToDelete)
                    {
                        FabDB.DeleteSupplierGroup(sg);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveProductCosts();
                    break;

                case "Costs":
                    foreach (var sg in FabDB.SupplierGroups)
                    {
                        var plToDelete = sg.PriceLists.Where(pl => nameSet.Contains(pl.Name)).ToList();
                        foreach (var pl in plToDelete)
                        {
                            sg.DeletePriceList(pl);
                            deleted++;
                        }
                    }
                    if (deleted > 0) FabDB.SaveProductCosts();
                    break;

                case "InstallationTimes":
                    var installToDelete = FabDB.InstallationTimesTable.Where(t => nameSet.Contains(t.Name)).ToList();
                    foreach (var t in installToDelete)
                    {
                        FabDB.DeleteInstallationTimesTable(t);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveInstallationTimes();
                    break;

                case "FabricationTimes":
                    var fabToDelete = FabDB.FabricationTimesTable.Where(t => nameSet.Contains(t.Name)).ToList();
                    foreach (var t in fabToDelete)
                    {
                        FabDB.DeleteFabricationTimesTable(t);
                        deleted++;
                    }
                    if (deleted > 0) FabDB.SaveFabricationTimes();
                    break;
            }

            return deleted;
        }
    }
}
