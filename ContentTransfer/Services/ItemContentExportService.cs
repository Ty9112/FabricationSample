using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;

using FabricationSample.ContentTransfer.Models;

namespace FabricationSample.ContentTransfer.Services
{
    public class ItemContentExportService
    {
        public event EventHandler<ExportProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Exports the specified item files to the output folder with a JSON manifest.
        /// </summary>
        /// <param name="itemPaths">Full paths to .itm files to export.</param>
        /// <param name="outputFolder">Destination folder for the package.</param>
        /// <returns>The generated ContentPackage manifest.</returns>
        public ContentPackage ExportItems(List<string> itemPaths, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var package = new ContentPackage
            {
                ConfigurationName = GetConfigurationName(),
                ExportedBy = Environment.UserName,
                ExportedAt = DateTime.UtcNow
            };

            int total = itemPaths.Count;
            for (int i = 0; i < total; i++)
            {
                string itemPath = itemPaths[i];
                string fileName = Path.GetFileName(itemPath);

                ReportProgress(i, total, $"Exporting {fileName}...");

                try
                {
                    var exportedItem = ExportSingleItem(itemPath, outputFolder);
                    if (exportedItem != null)
                        package.Items.Add(exportedItem);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to export {fileName}: {ex.Message}");
                }
            }

            // Save manifest
            SaveManifest(package, outputFolder);

            ReportProgress(total, total, "Export complete.");
            return package;
        }

        private ExportedItem ExportSingleItem(string itemPath, string outputFolder)
        {
            string fileName = Path.GetFileName(itemPath);

            // Load item via Fabrication API
            Item item = ContentManager.LoadItem(itemPath);
            if (item == null)
                return null;

            // Build references
            var refs = new ItemReferences();

            try { refs.ServiceName = item.Service?.Name; } catch { }
            try { refs.MaterialName = item.Material?.Name; } catch { }
            try { refs.SpecificationName = item.Specification?.Name; } catch { }
            try { refs.SectionDescription = item.Section?.Description; } catch { }
            try { refs.PriceListName = item.PriceList?.Name; } catch { }
            try { refs.InstallationTimesTableName = item.InstallationTimesTable?.Name; } catch { }
            try { refs.FabricationTimesTableName = item.FabricationTimesTable?.Name; } catch { }

            // Try to capture supplier group name from the price list's parent
            try
            {
                if (item.PriceList != null)
                {
                    foreach (var sg in Autodesk.Fabrication.DB.Database.SupplierGroups)
                    {
                        if (sg.PriceLists.Any(pl => pl.Name == item.PriceList.Name))
                        {
                            refs.SupplierGroupName = sg.Name;
                            break;
                        }
                    }
                }
            }
            catch { }

            // Build exported item
            var exported = new ExportedItem
            {
                FileName = fileName,
                SourceFolder = GetRelativeItemFolder(itemPath),
                References = refs
            };

            try { exported.CID = item.CID; } catch { }
            try { exported.DatabaseId = item.DatabaseId; } catch { }
            try { exported.IsProductList = item.IsProductList; } catch { }

            // Capture product list data if applicable
            try
            {
                if (item.IsProductList && item.ProductList?.Rows != null)
                {
                    exported.ProductList = CaptureProductList(item);
                }
            }
            catch { }

            // Copy .itm file to output folder
            string destPath = Path.Combine(outputFolder, fileName);
            File.Copy(itemPath, destPath, true);

            // Copy companion .png file if it exists
            string pngSource = Path.ChangeExtension(itemPath, ".png");
            if (File.Exists(pngSource))
            {
                string pngDest = Path.Combine(outputFolder, Path.ChangeExtension(fileName, ".png"));
                File.Copy(pngSource, pngDest, true);
            }

            return exported;
        }

        private ExportedProductList CaptureProductList(Item item)
        {
            var pl = new ExportedProductList();

            try { pl.Revision = item.ProductList.Revision; } catch { }

            foreach (var row in item.ProductList.Rows)
            {
                var exportedRow = new ExportedProductRow();

                try { exportedRow.Name = row.Name; } catch { }
                try { exportedRow.Alias = row.Alias; } catch { }
                try { exportedRow.DatabaseId = row.DatabaseId; } catch { }
                try { exportedRow.OrderNumber = row.OrderNumber; } catch { }
                try { exportedRow.BoughtOut = row.BoughtOut ?? false; } catch { }
                try { exportedRow.Weight = row.Weight; } catch { }

                pl.Rows.Add(exportedRow);
            }

            return pl;
        }

        private string GetConfigurationName()
        {
            try
            {
                string dbPath = Autodesk.Fabrication.ApplicationServices.Application.DatabasePath;
                // If it's a named profile, the path is {root}/profiles/{Name}/DATABASE
                // Extract the profile name from the path
                var parts = dbPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                int profilesIdx = Array.FindIndex(parts, p => p.Equals("profiles", StringComparison.OrdinalIgnoreCase));
                if (profilesIdx >= 0 && profilesIdx + 1 < parts.Length)
                    return parts[profilesIdx + 1];

                return "Global";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetRelativeItemFolder(string itemPath)
        {
            try
            {
                // Get the items root path from the folder hierarchy
                var folders = ItemFolders.Folders;
                if (folders != null && folders.Count > 0)
                {
                    // Find which folder this item belongs to
                    string itemDir = Path.GetDirectoryName(itemPath);
                    string firstFolderDir = folders[0].Directory;
                    string itemsRoot = Path.GetDirectoryName(firstFolderDir);

                    if (itemDir.StartsWith(itemsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return itemDir.Substring(itemsRoot.Length).TrimStart('\\', '/');
                    }
                }
            }
            catch { }

            return Path.GetDirectoryName(itemPath);
        }

        private void SaveManifest(ContentPackage package, string outputFolder)
        {
            string manifestPath = Path.Combine(outputFolder, "manifest.json");
            var serializer = new DataContractJsonSerializer(typeof(ContentPackage));

            using (var stream = File.Create(manifestPath))
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true))
                {
                    serializer.WriteObject(writer, package);
                }
            }
        }

        private void ReportProgress(int current, int total, string message)
        {
            ProgressChanged?.Invoke(this, new ExportProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message
            });
        }
    }

    public class ExportProgressEventArgs : EventArgs
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
    }
}
