using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

using Autodesk.Fabrication;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;

using FabricationSample.ContentTransfer.Models;

using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.ContentTransfer.Services
{
    public class ItemContentImportService
    {
        public event EventHandler<ImportProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Loads a ContentPackage manifest from the specified folder.
        /// </summary>
        public ContentPackage LoadPackage(string folderPath)
        {
            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            var serializer = new DataContractJsonSerializer(typeof(ContentPackage));
            using (var stream = File.OpenRead(manifestPath))
            {
                return serializer.ReadObject(stream) as ContentPackage;
            }
        }

        /// <summary>
        /// Validates which references in the package can be resolved against the current database.
        /// Returns one result per item with warnings for unresolvable references.
        /// </summary>
        public List<ItemImportResult> ValidatePackage(ContentPackage package)
        {
            var results = new List<ItemImportResult>();

            foreach (var item in package.Items)
            {
                var result = new ItemImportResult
                {
                    FileName = item.FileName,
                    Success = true
                };

                ValidateReferences(item.References, result);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Checks for duplicate DatabaseIds between the package items and existing items in the target folder.
        /// Returns a list of (fileName, databaseId, conflictingExistingPath) tuples.
        /// </summary>
        public List<DuplicateDatabaseIdInfo> CheckDuplicateDatabaseIds(ContentPackage package, string targetItemFolder)
        {
            var duplicates = new List<DuplicateDatabaseIdInfo>();

            // Build a set of DatabaseIds from the package items
            var packageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in package.Items)
            {
                if (!string.IsNullOrEmpty(item.DatabaseId))
                    packageIds[item.DatabaseId] = item.FileName;
            }

            if (packageIds.Count == 0)
                return duplicates;

            // Scan existing .itm files in the target folder for matching DatabaseIds
            if (Directory.Exists(targetItemFolder))
            {
                foreach (string existingPath in Directory.GetFiles(targetItemFolder, "*.itm", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        Item existingItem = ContentManager.LoadItem(existingPath);
                        if (existingItem != null && !string.IsNullOrEmpty(existingItem.DatabaseId))
                        {
                            string existingDbId = existingItem.DatabaseId;
                            if (packageIds.ContainsKey(existingDbId))
                            {
                                duplicates.Add(new DuplicateDatabaseIdInfo
                                {
                                    ImportFileName = packageIds[existingDbId],
                                    DatabaseId = existingDbId,
                                    ExistingFilePath = existingPath
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            return duplicates;
        }

        /// <summary>
        /// Imports the selected items from the package into the target folder.
        /// </summary>
        /// <param name="package">The loaded content package.</param>
        /// <param name="packageFolder">Folder containing the .itm files and manifest.</param>
        /// <param name="targetItemFolder">Target ItemFolder directory to place items in.</param>
        /// <param name="selectedIndices">Indices into package.Items to import. If null, imports all.</param>
        /// <param name="overridesPerItem">Optional per-item reference overrides (keyed by item index). Null to skip.</param>
        public List<ItemImportResult> ImportItems(ContentPackage package, string packageFolder,
            string targetItemFolder, List<int> selectedIndices,
            Dictionary<int, ReferenceOverrides> overridesPerItem = null)
        {
            var results = new List<ItemImportResult>();
            var indices = selectedIndices ?? Enumerable.Range(0, package.Items.Count).ToList();
            int total = indices.Count;

            for (int i = 0; i < total; i++)
            {
                int idx = indices[i];
                var exportedItem = package.Items[idx];

                ReportProgress(i, total, $"Importing {exportedItem.FileName}...");

                ReferenceOverrides overrides = null;
                overridesPerItem?.TryGetValue(idx, out overrides);

                var result = ImportSingleItem(exportedItem, packageFolder, targetItemFolder, overrides);
                results.Add(result);
            }

            ReportProgress(total, total, "Import complete.");
            return results;
        }

        private ItemImportResult ImportSingleItem(ExportedItem exportedItem, string packageFolder,
            string targetItemFolder, ReferenceOverrides overrides)
        {
            var result = new ItemImportResult
            {
                FileName = exportedItem.FileName,
                Success = false
            };

            try
            {
                // Copy .itm file to target folder
                string sourcePath = Path.Combine(packageFolder, exportedItem.FileName);
                string destPath = Path.Combine(targetItemFolder, exportedItem.FileName);

                if (!File.Exists(sourcePath))
                {
                    result.Errors.Add($"Source file not found: {exportedItem.FileName}");
                    return result;
                }

                File.Copy(sourcePath, destPath, true);

                // Copy companion .png file if it exists
                string pngSource = Path.ChangeExtension(sourcePath, ".png");
                if (File.Exists(pngSource))
                {
                    string pngDest = Path.ChangeExtension(destPath, ".png");
                    File.Copy(pngSource, pngDest, true);
                }

                // Load the item from its new location
                Item item = ContentManager.LoadItem(destPath);
                if (item == null)
                {
                    result.Errors.Add("Failed to load item after copy.");
                    return result;
                }

                // Re-resolve references by name (with overrides applied)
                ResolveReferences(item, exportedItem.References, result, overrides);

                // Save the item to persist re-resolved references
                try
                {
                    ContentManager.SaveItem(item);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"SaveItem failed: {ex.Message}. Trying SaveItemAs...");
                    try
                    {
                        string itemName = Path.GetFileNameWithoutExtension(exportedItem.FileName);
                        ContentManager.SaveItemAs(item, targetItemFolder, itemName, true);
                    }
                    catch (Exception ex2)
                    {
                        result.Errors.Add($"Failed to save item: {ex2.Message}");
                        return result;
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Import failed: {ex.Message}");
            }

            return result;
        }

        private void ResolveReferences(Item item, ItemReferences refs, ItemImportResult result,
            ReferenceOverrides overrides)
        {
            if (refs == null)
                return;

            // Material - use override if present
            string materialName = overrides?.GetOverride("Material") ?? refs.MaterialName;
            ResolveMaterial(item, materialName, result);

            // Specification
            string specName = overrides?.GetOverride("Specification") ?? refs.SpecificationName;
            ResolveSpecification(item, specName, result);

            // Section
            string sectionDesc = overrides?.GetOverride("Section") ?? refs.SectionDescription;
            ResolveSection(item, sectionDesc, result);

            // Price List
            string priceListName = overrides?.GetOverride("PriceList") ?? refs.PriceListName;
            ResolvePriceList(item, priceListName, result);

            // Installation Times Table
            string installTimesName = overrides?.GetOverride("InstallationTimesTable") ?? refs.InstallationTimesTableName;
            ResolveInstallationTimesTable(item, installTimesName, result);

            // Fabrication Times Table
            string fabTimesName = overrides?.GetOverride("FabricationTimesTable") ?? refs.FabricationTimesTableName;
            ResolveFabricationTimesTable(item, fabTimesName, result);

            // Service - report only (read-only on items)
            if (!string.IsNullOrEmpty(refs.ServiceName))
            {
                try
                {
                    var service = FabDB.Services.FirstOrDefault(s => s.Name == refs.ServiceName);
                    if (service == null)
                        result.Warnings.Add($"Service '{refs.ServiceName}' not found in target config (report-only, cannot re-assign).");
                }
                catch { }
            }
        }

        private void ResolveMaterial(Item item, string materialName, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(materialName))
                return;

            try
            {
                var material = FabDB.Materials.FirstOrDefault(m => m.Name == materialName);
                if (material != null)
                {
                    item.ChangeMaterial(material, null, false);
                }
                else
                {
                    result.Warnings.Add($"Material '{materialName}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving material '{materialName}': {ex.Message}");
            }
        }

        private void ResolveSpecification(Item item, string specName, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(specName))
                return;

            try
            {
                var spec = FabDB.Specifications.FirstOrDefault(s => s.Name == specName);
                if (spec != null)
                {
                    item.ChangeSpecification(spec, false);
                }
                else
                {
                    result.Warnings.Add($"Specification '{specName}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving specification '{specName}': {ex.Message}");
            }
        }

        private void ResolveSection(Item item, string sectionDesc, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(sectionDesc))
                return;

            try
            {
                var section = FabDB.Sections.FirstOrDefault(s => s.Description == sectionDesc);
                if (section != null)
                {
                    item.Section = section;
                }
                else
                {
                    result.Warnings.Add($"Section '{sectionDesc}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving section '{sectionDesc}': {ex.Message}");
            }
        }

        private void ResolvePriceList(Item item, string priceListName, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(priceListName))
                return;

            try
            {
                var priceList = FabDB.SupplierGroups
                    .SelectMany(sg => sg.PriceLists)
                    .FirstOrDefault(pl => pl.Name == priceListName);

                if (priceList != null)
                {
                    item.PriceList = priceList;
                }
                else
                {
                    result.Warnings.Add($"Price List '{priceListName}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving price list '{priceListName}': {ex.Message}");
            }
        }

        private void ResolveInstallationTimesTable(Item item, string tableName, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(tableName))
                return;

            try
            {
                var table = FabDB.InstallationTimesTable
                    .FirstOrDefault(t => t.Name == tableName);

                if (table != null)
                {
                    item.InstallationTimesTable = table;
                }
                else
                {
                    result.Warnings.Add($"Installation Times Table '{tableName}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving installation times table '{tableName}': {ex.Message}");
            }
        }

        private void ResolveFabricationTimesTable(Item item, string tableName, ItemImportResult result)
        {
            if (string.IsNullOrEmpty(tableName))
                return;

            try
            {
                var table = FabDB.FabricationTimesTable
                    .FirstOrDefault(t => t.Name == tableName);

                if (table != null)
                {
                    item.FabricationTimesTable = table;
                }
                else
                {
                    result.Warnings.Add($"Fabrication Times Table '{tableName}' not found.");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Error resolving fabrication times table '{tableName}': {ex.Message}");
            }
        }

        private void ValidateReferences(ItemReferences refs, ItemImportResult result)
        {
            if (refs == null)
                return;

            // Service
            if (!string.IsNullOrEmpty(refs.ServiceName))
            {
                try
                {
                    var service = FabDB.Services.FirstOrDefault(s => s.Name == refs.ServiceName);
                    if (service == null)
                        result.Warnings.Add($"Service '{refs.ServiceName}' not found (report-only).");
                }
                catch { }
            }

            // Material
            if (!string.IsNullOrEmpty(refs.MaterialName))
            {
                try
                {
                    var material = FabDB.Materials.FirstOrDefault(m => m.Name == refs.MaterialName);
                    if (material == null)
                        result.Warnings.Add($"Material '{refs.MaterialName}' not found.");
                }
                catch { }
            }

            // Specification
            if (!string.IsNullOrEmpty(refs.SpecificationName))
            {
                try
                {
                    var spec = FabDB.Specifications.FirstOrDefault(s => s.Name == refs.SpecificationName);
                    if (spec == null)
                        result.Warnings.Add($"Specification '{refs.SpecificationName}' not found.");
                }
                catch { }
            }

            // Section
            if (!string.IsNullOrEmpty(refs.SectionDescription))
            {
                try
                {
                    var section = FabDB.Sections.FirstOrDefault(s => s.Description == refs.SectionDescription);
                    if (section == null)
                        result.Warnings.Add($"Section '{refs.SectionDescription}' not found.");
                }
                catch { }
            }

            // Price List
            if (!string.IsNullOrEmpty(refs.PriceListName))
            {
                try
                {
                    var priceList = FabDB.SupplierGroups
                        .SelectMany(sg => sg.PriceLists)
                        .FirstOrDefault(pl => pl.Name == refs.PriceListName);
                    if (priceList == null)
                        result.Warnings.Add($"Price List '{refs.PriceListName}' not found.");
                }
                catch { }
            }

            // Installation Times Table
            if (!string.IsNullOrEmpty(refs.InstallationTimesTableName))
            {
                try
                {
                    var table = FabDB.InstallationTimesTable
                        .FirstOrDefault(t => t.Name == refs.InstallationTimesTableName);
                    if (table == null)
                        result.Warnings.Add($"Installation Times Table '{refs.InstallationTimesTableName}' not found.");
                }
                catch { }
            }

            // Fabrication Times Table
            if (!string.IsNullOrEmpty(refs.FabricationTimesTableName))
            {
                try
                {
                    var table = FabDB.FabricationTimesTable
                        .FirstOrDefault(t => t.Name == refs.FabricationTimesTableName);
                    if (table == null)
                        result.Warnings.Add($"Fabrication Times Table '{refs.FabricationTimesTableName}' not found.");
                }
                catch { }
            }
        }

        private void ReportProgress(int current, int total, string message)
        {
            ProgressChanged?.Invoke(this, new ImportProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message
            });
        }
    }

    public class DuplicateDatabaseIdInfo
    {
        public string ImportFileName { get; set; }
        public string DatabaseId { get; set; }
        public string ExistingFilePath { get; set; }
    }

    public class ImportProgressEventArgs : EventArgs
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
    }
}
