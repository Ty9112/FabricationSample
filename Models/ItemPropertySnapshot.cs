using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Models
{
    /// <summary>
    /// Options for which properties to transfer during an item swap.
    /// </summary>
    public class ItemSwapOptions
    {
        /// <summary>
        /// Transfer position (XYZ coordinates). Default: true
        /// </summary>
        public bool TransferPosition { get; set; } = true;

        /// <summary>
        /// Transfer matching dimension values. Default: true
        /// </summary>
        public bool TransferDimensions { get; set; } = true;

        /// <summary>
        /// Transfer matching option values. Default: true
        /// </summary>
        public bool TransferOptions { get; set; } = true;

        /// <summary>
        /// Transfer custom data values. Default: true
        /// </summary>
        public bool TransferCustomData { get; set; } = true;

        /// <summary>
        /// Transfer basic info (Notes, Order, Zone, ETag, etc.). Default: true
        /// </summary>
        public bool TransferBasicInfo { get; set; } = true;

        /// <summary>
        /// Transfer status and section. Default: true
        /// </summary>
        public bool TransferStatusSection { get; set; } = true;

        /// <summary>
        /// Transfer price list assignment. Default: false
        /// </summary>
        public bool TransferPriceList { get; set; } = false;

        /// <summary>
        /// Transfer service type. Default: false
        /// </summary>
        public bool TransferServiceType { get; set; } = false;
    }

    /// <summary>
    /// Result of a property transfer operation.
    /// </summary>
    public class PropertyTransferResult
    {
        /// <summary>
        /// Whether the transfer was successful overall.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of dimensions transferred.
        /// </summary>
        public int DimensionsTransferred { get; set; }

        /// <summary>
        /// Number of options transferred.
        /// </summary>
        public int OptionsTransferred { get; set; }

        /// <summary>
        /// Number of custom data fields transferred.
        /// </summary>
        public int CustomDataTransferred { get; set; }

        /// <summary>
        /// Dimensions that could not be transferred (name mismatch or invalid value).
        /// </summary>
        public List<string> SkippedDimensions { get; set; } = new List<string>();

        /// <summary>
        /// Options that could not be transferred.
        /// </summary>
        public List<string> SkippedOptions { get; set; } = new List<string>();

        /// <summary>
        /// Any errors encountered during transfer.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Summary message of the transfer.
        /// </summary>
        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (DimensionsTransferred > 0)
                    parts.Add($"{DimensionsTransferred} dimensions");
                if (OptionsTransferred > 0)
                    parts.Add($"{OptionsTransferred} options");
                if (CustomDataTransferred > 0)
                    parts.Add($"{CustomDataTransferred} custom data fields");

                if (parts.Count == 0)
                    return "No properties transferred";

                return $"Transferred: {string.Join(", ", parts)}";
            }
        }
    }

    /// <summary>
    /// Helper class for transferring properties between items.
    /// </summary>
    public static class ItemPropertyTransfer
    {
        /// <summary>
        /// Transfers properties from an undo record to a new item.
        /// </summary>
        /// <param name="undoRecord">The undo record containing original property values.</param>
        /// <param name="targetItem">The new item to transfer properties to.</param>
        /// <param name="options">Options specifying which properties to transfer.</param>
        /// <returns>Result of the transfer operation.</returns>
        public static PropertyTransferResult TransferProperties(
            ItemSwapUndoRecord undoRecord,
            Item targetItem,
            ItemSwapOptions options)
        {
            var result = new PropertyTransferResult { Success = true };

            if (undoRecord == null || targetItem == null)
            {
                result.Success = false;
                result.Errors.Add("Invalid undo record or target item");
                return result;
            }

            try
            {
                // Transfer basic info
                if (options.TransferBasicInfo)
                {
                    TransferBasicInfo(undoRecord, targetItem, result);
                }

                // Transfer dimensions
                if (options.TransferDimensions)
                {
                    TransferDimensions(undoRecord, targetItem, result);
                }

                // Transfer options
                if (options.TransferOptions)
                {
                    TransferOptions(undoRecord, targetItem, result);
                }

                // Transfer custom data
                if (options.TransferCustomData)
                {
                    TransferCustomData(undoRecord, targetItem, result);
                }

                // Transfer status and section
                if (options.TransferStatusSection)
                {
                    TransferStatusSection(undoRecord, targetItem, result);
                }

                // Transfer price list
                if (options.TransferPriceList)
                {
                    TransferPriceList(undoRecord, targetItem, result);
                }

                // Update the item to recalculate
                targetItem.Update();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error during property transfer: {ex.Message}");
            }

            return result;
        }

        private static void TransferBasicInfo(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            try
            {
                if (!string.IsNullOrEmpty(record.OriginalNotes))
                    target.Notes = record.OriginalNotes;

                if (!string.IsNullOrEmpty(record.OriginalOrder))
                    target.Order = record.OriginalOrder;

                if (!string.IsNullOrEmpty(record.OriginalZone))
                    target.Zone = record.OriginalZone;

                if (!string.IsNullOrEmpty(record.OriginalEquipmentTag))
                    target.EquipmentTag = record.OriginalEquipmentTag;

                if (!string.IsNullOrEmpty(record.OriginalDrawingName))
                    target.DrawingName = record.OriginalDrawingName;

                if (!string.IsNullOrEmpty(record.OriginalPallet))
                    target.Pallet = record.OriginalPallet;

                if (!string.IsNullOrEmpty(record.OriginalAlias))
                    target.Alias = record.OriginalAlias;

                // Note: SKey is read-only, cannot be set

                target.IsHiddenInViews = record.OriginalIsHiddenInViews;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error transferring basic info: {ex.Message}");
            }
        }

        private static void TransferDimensions(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            foreach (var dimEntry in record.OriginalDimensions)
            {
                try
                {
                    // Find matching dimension by name
                    var targetDim = target.Dimensions.FirstOrDefault(d => d.Name == dimEntry.Key);
                    if (targetDim == null)
                    {
                        result.SkippedDimensions.Add(dimEntry.Key);
                        continue;
                    }

                    // Set the dimension value based on type
                    if (targetDim is ItemDimension itemDim)
                    {
                        itemDim.Value = dimEntry.Value;
                        result.DimensionsTransferred++;
                    }
                    else if (targetDim is ItemComboDimension comboDim)
                    {
                        // For combo dimensions, find and select the value entry
                        var valueEntry = comboDim.Options
                            .OfType<ItemComboDimensionValueEntry>()
                            .FirstOrDefault();

                        if (valueEntry != null)
                        {
                            valueEntry.IsSelected = true;
                            valueEntry.Value = dimEntry.Value;
                            result.DimensionsTransferred++;
                        }
                        else
                        {
                            result.SkippedDimensions.Add($"{dimEntry.Key} (combo has no value entry)");
                        }
                    }
                }
                catch (Exception)
                {
                    result.SkippedDimensions.Add(dimEntry.Key);
                }
            }
        }

        private static void TransferOptions(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            foreach (var optEntry in record.OriginalOptions)
            {
                try
                {
                    // Find matching option by name
                    var targetOpt = target.Options.FirstOrDefault(o => o.Name == optEntry.Key);
                    if (targetOpt == null)
                    {
                        result.SkippedOptions.Add(optEntry.Key);
                        continue;
                    }

                    // Set the option value based on type using ChangeValue method
                    if (targetOpt is ItemMinMaxNumericOption numOpt && optEntry.Value is double doubleVal)
                    {
                        if (doubleVal >= numOpt.Lowest && doubleVal <= numOpt.Highest)
                        {
                            numOpt.ChangeValue(doubleVal);
                            result.OptionsTransferred++;
                        }
                        else
                        {
                            result.SkippedOptions.Add($"{optEntry.Key} (value out of range)");
                        }
                    }
                    else if (targetOpt is ItemMinMaxIntegerOption intOpt && optEntry.Value is int intVal)
                    {
                        if (intVal >= intOpt.Lowest && intVal <= intOpt.Highest)
                        {
                            intOpt.ChangeValue(intVal);
                            result.OptionsTransferred++;
                        }
                        else
                        {
                            result.SkippedOptions.Add($"{optEntry.Key} (value out of range)");
                        }
                    }
                    else if (targetOpt is ItemSelectOption selectOpt && optEntry.Value != null)
                    {
                        // For select options, find the matching entry and select it
                        // The value is typically the index or name
                        result.SkippedOptions.Add($"{optEntry.Key} (select option - manual transfer required)");
                    }
                    else if (targetOpt is ItemComboOption comboOpt && optEntry.Value != null)
                    {
                        // For combo options, complex handling would be needed
                        result.SkippedOptions.Add($"{optEntry.Key} (combo option - manual transfer required)");
                    }
                }
                catch (Exception)
                {
                    result.SkippedOptions.Add(optEntry.Key);
                }
            }
        }

        private static void TransferCustomData(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            foreach (var cdEntry in record.OriginalCustomData)
            {
                try
                {
                    // Find matching custom data by ID
                    var targetCd = target.CustomData.FirstOrDefault(cd => cd.Data.Id == cdEntry.Key);
                    if (targetCd == null)
                        continue;

                    // Set value based on type
                    switch (targetCd.Data.Type)
                    {
                        case CustomDataType.String:
                            var sVal = targetCd as CustomDataStringValue;
                            if (sVal != null)
                            {
                                sVal.Value = cdEntry.Value;
                                result.CustomDataTransferred++;
                            }
                            break;
                        case CustomDataType.Integer:
                            var iVal = targetCd as CustomDataIntegerValue;
                            if (iVal != null && int.TryParse(cdEntry.Value, out int intValue))
                            {
                                iVal.Value = intValue;
                                result.CustomDataTransferred++;
                            }
                            break;
                        case CustomDataType.Double:
                            var dVal = targetCd as CustomDataDoubleValue;
                            if (dVal != null && double.TryParse(cdEntry.Value, out double doubleValue))
                            {
                                dVal.Value = doubleValue;
                                result.CustomDataTransferred++;
                            }
                            break;
                    }
                }
                catch (Exception)
                {
                    // Skip this custom data field
                }
            }
        }

        private static void TransferStatusSection(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            try
            {
                // Transfer status
                if (record.OriginalStatusId.HasValue)
                {
                    var status = Database.ItemStatuses.FirstOrDefault(s => s.Id == record.OriginalStatusId.Value);
                    if (status != null)
                        target.Status = status;
                }

                // Transfer section - match by description since Index might change
                if (!string.IsNullOrEmpty(record.OriginalSectionDescription))
                {
                    var section = Database.Sections.FirstOrDefault(s => s.Description == record.OriginalSectionDescription);
                    if (section != null)
                        target.Section = section;
                }
                else if (record.OriginalSectionIndex.HasValue)
                {
                    var section = Database.Sections.FirstOrDefault(s => s.Index == record.OriginalSectionIndex.Value);
                    if (section != null)
                        target.Section = section;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error transferring status/section: {ex.Message}");
            }
        }

        private static void TransferPriceList(ItemSwapUndoRecord record, Item target, PropertyTransferResult result)
        {
            try
            {
                if (!string.IsNullOrEmpty(record.OriginalPriceListName))
                {
                    var priceList = Database.SupplierGroups
                        .SelectMany(sg => sg.PriceLists)
                        .FirstOrDefault(pl => pl.Name == record.OriginalPriceListName);

                    if (priceList != null)
                        target.PriceList = priceList;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error transferring price list: {ex.Message}");
            }
        }
    }
}
