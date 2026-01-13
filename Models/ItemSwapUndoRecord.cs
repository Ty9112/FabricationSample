using System;
using System.Collections.Generic;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Models
{
    /// <summary>
    /// Records all data needed to undo an item swap operation.
    /// Contains a complete snapshot of the original item's state.
    /// </summary>
    public class ItemSwapUndoRecord
    {
        /// <summary>
        /// Timestamp when the swap occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Description of the swap for display purposes.
        /// </summary>
        public string Description { get; set; }

        #region Original Item Identification

        /// <summary>
        /// Path to the original ITM file.
        /// </summary>
        public string OriginalItemPath { get; set; }

        /// <summary>
        /// Name of the original item.
        /// </summary>
        public string OriginalItemName { get; set; }

        /// <summary>
        /// Name of the service the original item belonged to.
        /// </summary>
        public string OriginalServiceName { get; set; }

        /// <summary>
        /// Name of the service button the original item came from.
        /// </summary>
        public string OriginalButtonName { get; set; }

        /// <summary>
        /// Index of the service button item.
        /// </summary>
        public int OriginalButtonItemIndex { get; set; }

        #endregion

        #region Original Position Data

        /// <summary>
        /// Position data of the original item for restoration.
        /// </summary>
        public ItemPositionData OriginalPosition { get; set; }

        #endregion

        #region Original Property Values

        /// <summary>
        /// Original dimension values keyed by dimension name.
        /// </summary>
        public Dictionary<string, double> OriginalDimensions { get; set; }

        /// <summary>
        /// Original option values keyed by option name.
        /// Value is object to support different option types (int, string, etc.)
        /// </summary>
        public Dictionary<string, object> OriginalOptions { get; set; }

        /// <summary>
        /// Original custom data values keyed by custom data ID.
        /// </summary>
        public Dictionary<int, string> OriginalCustomData { get; set; }

        /// <summary>
        /// Original notes.
        /// </summary>
        public string OriginalNotes { get; set; }

        /// <summary>
        /// Original order number.
        /// </summary>
        public string OriginalOrder { get; set; }

        /// <summary>
        /// Original zone.
        /// </summary>
        public string OriginalZone { get; set; }

        /// <summary>
        /// Original equipment tag.
        /// </summary>
        public string OriginalEquipmentTag { get; set; }

        /// <summary>
        /// Original drawing name.
        /// </summary>
        public string OriginalDrawingName { get; set; }

        /// <summary>
        /// Original pallet.
        /// </summary>
        public string OriginalPallet { get; set; }

        /// <summary>
        /// Original SKey.
        /// </summary>
        public string OriginalSKey { get; set; }

        /// <summary>
        /// Original alias.
        /// </summary>
        public string OriginalAlias { get; set; }

        /// <summary>
        /// Original item status ID.
        /// </summary>
        public int? OriginalStatusId { get; set; }

        /// <summary>
        /// Original section index.
        /// </summary>
        public int? OriginalSectionIndex { get; set; }

        /// <summary>
        /// Original section description for matching.
        /// </summary>
        public string OriginalSectionDescription { get; set; }

        /// <summary>
        /// Original service type ID.
        /// </summary>
        public int? OriginalServiceTypeId { get; set; }

        /// <summary>
        /// Original price list name.
        /// </summary>
        public string OriginalPriceListName { get; set; }

        /// <summary>
        /// Original visibility state.
        /// </summary>
        public bool OriginalIsHiddenInViews { get; set; }

        /// <summary>
        /// Original item number.
        /// </summary>
        public string OriginalNumber { get; set; }

        /// <summary>
        /// Original CID (pattern number).
        /// </summary>
        public int OriginalCID { get; set; }

        /// <summary>
        /// Original product list entry name (if item has a product list).
        /// </summary>
        public string OriginalProductListEntryName { get; set; }

        /// <summary>
        /// Original AutoCAD handle for position operations.
        /// </summary>
        public string OriginalAcadHandle { get; set; }

        #endregion

        #region New Item Reference

        /// <summary>
        /// Unique ID of the new item (for deletion during undo).
        /// </summary>
        public string NewItemUniqueId { get; set; }

        /// <summary>
        /// Name of the new item.
        /// </summary>
        public string NewItemName { get; set; }

        #endregion

        /// <summary>
        /// Creates a new instance of ItemSwapUndoRecord.
        /// </summary>
        public ItemSwapUndoRecord()
        {
            Timestamp = DateTime.Now;
            OriginalDimensions = new Dictionary<string, double>();
            OriginalOptions = new Dictionary<string, object>();
            OriginalCustomData = new Dictionary<int, string>();
        }

        /// <summary>
        /// Creates an undo record by capturing all properties from the original item.
        /// </summary>
        /// <param name="originalItem">The original item being replaced.</param>
        /// <returns>An ItemSwapUndoRecord with all original item data.</returns>
        public static ItemSwapUndoRecord CaptureFromItem(Item originalItem)
        {
            if (originalItem == null)
                return null;

            var record = new ItemSwapUndoRecord
            {
                Timestamp = DateTime.Now,
                OriginalItemName = originalItem.Name,
                OriginalPosition = ItemPositionData.CaptureFromItem(originalItem)
            };

            try
            {
                // Capture basic properties
                record.OriginalNotes = originalItem.Notes;
                record.OriginalOrder = originalItem.Order;
                record.OriginalZone = originalItem.Zone;
                record.OriginalEquipmentTag = originalItem.EquipmentTag;
                record.OriginalDrawingName = originalItem.DrawingName;
                record.OriginalPallet = originalItem.Pallet;
                record.OriginalAlias = originalItem.Alias;
                record.OriginalIsHiddenInViews = originalItem.IsHiddenInViews;
                record.OriginalNumber = originalItem.Number;
                record.OriginalCID = originalItem.CID;

                // Capture product list entry name if applicable
                if (originalItem.IsProductList && originalItem.ProductList?.Rows != null)
                {
                    // Try to find which product list row matches the current dimensions
                    foreach (var row in originalItem.ProductList.Rows)
                    {
                        if (!string.IsNullOrEmpty(row.Name))
                        {
                            record.OriginalProductListEntryName = row.Name;
                            break;
                        }
                    }
                }

                // Capture AutoCAD handle for position operations
                try
                {
                    record.OriginalAcadHandle = Job.GetACADHandleFromItem(originalItem);
                }
                catch { }

                // Capture SKey if supported
                if (originalItem.SupportsSKey)
                    record.OriginalSKey = originalItem.SKey;

                // Capture status
                if (originalItem.Status != null)
                    record.OriginalStatusId = originalItem.Status.Id;

                // Capture section
                if (originalItem.Section != null)
                {
                    record.OriginalSectionIndex = originalItem.Section.Index;
                    record.OriginalSectionDescription = originalItem.Section.Description;
                }

                // Capture service type
                if (originalItem.ServiceType != null)
                    record.OriginalServiceTypeId = originalItem.ServiceType.Id;

                // Capture service info
                if (originalItem.Service != null)
                    record.OriginalServiceName = originalItem.Service.Name;

                // Capture price list
                if (originalItem.PriceList != null)
                    record.OriginalPriceListName = originalItem.PriceList.Name;

                // Capture dimensions
                foreach (var dim in originalItem.Dimensions)
                {
                    try
                    {
                        // ItemDimensionBase.Value is accessible for reading
                        record.OriginalDimensions[dim.Name] = dim.Value;
                    }
                    catch { }
                }

                // Capture options - using ItemOptionBase.Value which is accessible for reading
                foreach (var opt in originalItem.Options)
                {
                    try
                    {
                        record.OriginalOptions[opt.Name] = opt.Value;
                    }
                    catch { }
                }

                // Capture custom data
                foreach (var customData in originalItem.CustomData)
                {
                    try
                    {
                        record.OriginalCustomData[customData.Data.Id] = GetCustomDataValue(customData);
                    }
                    catch { }
                }

                record.Description = $"Swapped '{originalItem.Name}'";
            }
            catch (Exception)
            {
                // If we can't capture everything, return what we have
            }

            return record;
        }

        /// <summary>
        /// Gets the string value of a custom data entry.
        /// </summary>
        private static string GetCustomDataValue(CustomItemData customData)
        {
            try
            {
                switch (customData.Data.Type)
                {
                    case CustomDataType.String:
                        var sVal = customData as CustomDataStringValue;
                        return sVal?.Value ?? string.Empty;
                    case CustomDataType.Integer:
                        var iVal = customData as CustomDataIntegerValue;
                        return iVal?.Value.ToString() ?? string.Empty;
                    case CustomDataType.Double:
                        var dVal = customData as CustomDataDoubleValue;
                        return dVal?.Value.ToString() ?? string.Empty;
                    default:
                        return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
