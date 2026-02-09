using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FabricationSample.ContentTransfer.Models
{
    [DataContract]
    public class ContentPackage
    {
        [DataMember(Name = "configurationName")]
        public string ConfigurationName { get; set; }

        [DataMember(Name = "exportedBy")]
        public string ExportedBy { get; set; }

        [DataMember(Name = "exportedAt")]
        public DateTime ExportedAt { get; set; }

        [DataMember(Name = "items")]
        public List<ExportedItem> Items { get; set; }

        public ContentPackage()
        {
            Items = new List<ExportedItem>();
        }
    }

    [DataContract]
    public class ExportedItem
    {
        [DataMember(Name = "fileName")]
        public string FileName { get; set; }

        [DataMember(Name = "sourceFolder")]
        public string SourceFolder { get; set; }

        [DataMember(Name = "cid")]
        public int CID { get; set; }

        [DataMember(Name = "databaseId")]
        public string DatabaseId { get; set; }

        [DataMember(Name = "isProductList")]
        public bool IsProductList { get; set; }

        [DataMember(Name = "references")]
        public ItemReferences References { get; set; }

        [DataMember(Name = "productList")]
        public ExportedProductList ProductList { get; set; }
    }

    [DataContract]
    public class ItemReferences
    {
        [DataMember(Name = "serviceName")]
        public string ServiceName { get; set; }

        [DataMember(Name = "materialName")]
        public string MaterialName { get; set; }

        [DataMember(Name = "specificationName")]
        public string SpecificationName { get; set; }

        [DataMember(Name = "sectionDescription")]
        public string SectionDescription { get; set; }

        [DataMember(Name = "priceListName")]
        public string PriceListName { get; set; }

        [DataMember(Name = "supplierGroupName")]
        public string SupplierGroupName { get; set; }

        [DataMember(Name = "installationTimesTableName")]
        public string InstallationTimesTableName { get; set; }

        [DataMember(Name = "fabricationTimesTableName")]
        public string FabricationTimesTableName { get; set; }
    }

    [DataContract]
    public class ExportedProductList
    {
        [DataMember(Name = "revision")]
        public string Revision { get; set; }

        [DataMember(Name = "rows")]
        public List<ExportedProductRow> Rows { get; set; }

        public ExportedProductList()
        {
            Rows = new List<ExportedProductRow>();
        }
    }

    [DataContract]
    public class ExportedProductRow
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "alias")]
        public string Alias { get; set; }

        [DataMember(Name = "databaseId")]
        public string DatabaseId { get; set; }

        [DataMember(Name = "orderNumber")]
        public string OrderNumber { get; set; }

        [DataMember(Name = "boughtOut")]
        public bool BoughtOut { get; set; }

        [DataMember(Name = "weight")]
        public double? Weight { get; set; }
    }

    /// <summary>
    /// User-selected replacement references for unmatched properties per item index.
    /// Keys: "Material", "Specification", "Section", "PriceList", "InstallationTimesTable", "FabricationTimesTable"
    /// Values: the NAME the user picked from the target database (or null/empty to skip).
    /// </summary>
    public class ReferenceOverrides
    {
        public Dictionary<string, string> Overrides { get; set; }

        public ReferenceOverrides()
        {
            Overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string GetOverride(string key)
        {
            string val;
            if (Overrides.TryGetValue(key, out val))
                return val;
            return null;
        }
    }

    /// <summary>
    /// Tracks the result of importing a single item.
    /// </summary>
    public class ItemImportResult
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Errors { get; set; }

        public ItemImportResult()
        {
            Warnings = new List<string>();
            Errors = new List<string>();
        }
    }
}
