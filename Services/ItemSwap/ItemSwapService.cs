using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using Autodesk.Fabrication.Geometry;
using FabricationSample.Models;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace FabricationSample.Services.ItemSwap
{
    /// <summary>
    /// Result of an item swap operation.
    /// </summary>
    public class ItemSwapResult
    {
        /// <summary>
        /// Whether the swap was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the swap failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The new item that replaced the original.
        /// </summary>
        public Item NewItem { get; set; }

        /// <summary>
        /// Result of the property transfer.
        /// </summary>
        public PropertyTransferResult TransferResult { get; set; }

        /// <summary>
        /// The undo record created for this swap.
        /// </summary>
        public ItemSwapUndoRecord UndoRecord { get; set; }
    }

    /// <summary>
    /// Service for swapping fabrication items within the same service.
    /// Handles position retention, property transfer, and undo recording.
    /// </summary>
    public class ItemSwapService
    {
        private readonly ItemSwapUndoManager _undoManager;

        /// <summary>
        /// Creates a new instance of ItemSwapService.
        /// </summary>
        public ItemSwapService()
        {
            _undoManager = ItemSwapUndoManager.Instance;
        }

        /// <summary>
        /// Swaps an existing job item with a new item from the same service.
        /// </summary>
        /// <param name="originalItem">The item to be replaced.</param>
        /// <param name="newServiceButton">The service button containing the new item.</param>
        /// <param name="newButtonItemIndex">The index of the new item in the service button.</param>
        /// <param name="options">Options for which properties to transfer.</param>
        /// <returns>Result of the swap operation.</returns>
        public ItemSwapResult SwapItem(
            Item originalItem,
            ServiceButton newServiceButton,
            int newButtonItemIndex,
            ItemSwapOptions options)
        {
            var result = new ItemSwapResult();

            if (originalItem == null)
            {
                result.Success = false;
                result.ErrorMessage = "Original item is null.";
                return result;
            }

            if (newServiceButton == null)
            {
                result.Success = false;
                result.ErrorMessage = "Service button is null.";
                return result;
            }

            if (newButtonItemIndex < 0 || newButtonItemIndex >= newServiceButton.ServiceButtonItems.Count)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid button item index.";
                return result;
            }

            try
            {
                // Step 1: Capture undo record from original item
                var undoRecord = ItemSwapUndoRecord.CaptureFromItem(originalItem);
                if (undoRecord == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to capture original item data.";
                    return result;
                }

                // Store the original item's service button info using CID matching
                var (buttonName, itemPath) = FindOriginalButtonInfo(originalItem);
                undoRecord.OriginalButtonName = buttonName;
                undoRecord.OriginalItemPath = itemPath;

                // If we couldn't find the exact button, store enough info to search by CID later
                if (string.IsNullOrEmpty(buttonName) || string.IsNullOrEmpty(itemPath))
                {
                    // Store the new item's button as fallback (we know this from the swap selection)
                    // Note: This means undo will reload the same CID from any available button
                    undoRecord.OriginalButtonName = newServiceButton.Name;
                    undoRecord.OriginalItemPath = newServiceButton.ServiceButtonItems[newButtonItemIndex].ItemPath;
                }

                // Step 2: Load the new item
                var service = originalItem.Service;
                var newButtonItem = newServiceButton.ServiceButtonItems[newButtonItemIndex];

                ItemOperationResult loadResult = service.LoadServiceItem(newServiceButton, newButtonItem, true);
                if (loadResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to load new item: {loadResult.Message}";
                    return result;
                }

                Item newItem = loadResult.ReturnObject as Item;
                if (newItem == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to get new item from load result.";
                    return result;
                }

                // Step 3: Transfer properties from undo record to new item
                result.TransferResult = ItemPropertyTransfer.TransferProperties(undoRecord, newItem, options);

                // Step 4: Delete the original item
                ItemOperationResult deleteResult = Job.DeleteItem(originalItem);
                if (deleteResult.Status != ResultStatus.Succeeded)
                {
                    // Try to clean up the new item
                    try { Job.DeleteItem(newItem); } catch { }

                    result.Success = false;
                    result.ErrorMessage = $"Failed to delete original item: {deleteResult.Message}";
                    return result;
                }

                // Step 5: Add the new item to the job
                ItemOperationResult addResult = Job.AddItem(newItem);
                if (addResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to add new item to job: {addResult.Message}";
                    return result;
                }

                // Step 6: Position the new item at original location using connector endpoints
                if (options.TransferPosition && undoRecord.OriginalPosition != null && undoRecord.OriginalPosition.HasValidPosition)
                {
                    PositionItemUsingConnectors(newItem, undoRecord.OriginalPosition);
                }

                // Step 7: Update undo record with new item info and push to stack
                undoRecord.NewItemUniqueId = newItem.UniqueId;
                undoRecord.NewItemName = newItem.Name;
                undoRecord.Description = $"Swapped '{undoRecord.OriginalItemName}' with '{newItem.Name}'";
                _undoManager.RecordSwap(undoRecord);

                // Step 8: Update the view
                Autodesk.Fabrication.UI.UIApplication.UpdateView(new[] { newItem }.ToList());

                result.Success = true;
                result.NewItem = newItem;
                result.UndoRecord = undoRecord;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error during swap: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Undoes the last swap operation.
        /// </summary>
        /// <returns>Result of the undo operation.</returns>
        public ItemSwapResult UndoLastSwap()
        {
            var result = new ItemSwapResult();

            if (!_undoManager.CanUndo)
            {
                result.Success = false;
                result.ErrorMessage = "No swap operations to undo.";
                return result;
            }

            var undoRecord = _undoManager.PopUndo();
            if (undoRecord == null)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to get undo record.";
                return result;
            }

            try
            {
                // Step 1: Find and delete the new item
                Item newItem = null;
                foreach (Item item in Job.Items)
                {
                    if (item.UniqueId == undoRecord.NewItemUniqueId)
                    {
                        newItem = item;
                        break;
                    }
                }

                if (newItem != null)
                {
                    var deleteResult = Job.DeleteItem(newItem);
                    if (deleteResult.Status != ResultStatus.Succeeded)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Failed to delete swapped item: {deleteResult.Message}";
                        // Push the undo record back since we couldn't complete
                        _undoManager.RecordSwap(undoRecord);
                        return result;
                    }
                }

                // Step 2: Reload the original item
                Service service = Autodesk.Fabrication.DB.Database.Services.FirstOrDefault(s => s.Name == undoRecord.OriginalServiceName);
                if (service == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not find original service.";
                    return result;
                }

                // Find the original button and item - try multiple strategies
                ServiceButton originalButton = null;
                ServiceButtonItem originalButtonItem = null;

                // Strategy 1: Try exact button name and item path match
                // Convert to arrays to avoid collection modification during enumeration
                var serviceTabs = service.ServiceTemplate.ServiceTabs.ToArray();
                foreach (var tab in serviceTabs)
                {
                    var serviceButtons = tab.ServiceButtons.ToArray();
                    foreach (var button in serviceButtons)
                    {
                        if (button.Name == undoRecord.OriginalButtonName)
                        {
                            originalButton = button;
                            // Try to find the item by path
                            var buttonItems = button.ServiceButtonItems.ToArray();
                            for (int i = 0; i < buttonItems.Length; i++)
                            {
                                if (buttonItems[i].ItemPath == undoRecord.OriginalItemPath)
                                {
                                    originalButtonItem = buttonItems[i];
                                    break;
                                }
                            }
                            // If no path match, take the first item from this button
                            if (originalButtonItem == null && buttonItems.Length > 0)
                            {
                                originalButtonItem = buttonItems[0];
                            }
                            break;
                        }
                    }
                    if (originalButton != null) break;
                }

                // Strategy 2: Search by CID using filename matching (avoid loading items which modifies collections)
                if (originalButton == null || originalButtonItem == null)
                {
                    int targetCID = undoRecord.OriginalCID;
                    string targetName = undoRecord.OriginalItemName;
                    var allTabs = service.ServiceTemplate.ServiceTabs.ToArray();

                    foreach (var tab in allTabs)
                    {
                        var allButtons = tab.ServiceButtons.ToArray();
                        foreach (var button in allButtons)
                        {
                            var allButtonItems = button.ServiceButtonItems.ToArray();
                            foreach (var buttonItem in allButtonItems)
                            {
                                try
                                {
                                    // Match by filename containing CID or item name
                                    string fileName = System.IO.Path.GetFileNameWithoutExtension(buttonItem.ItemPath ?? "");
                                    if (!string.IsNullOrEmpty(fileName))
                                    {
                                        if (fileName.Contains(targetCID.ToString()) ||
                                            (!string.IsNullOrEmpty(targetName) &&
                                             fileName.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0))
                                        {
                                            originalButton = button;
                                            originalButtonItem = buttonItem;
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                            if (originalButton != null) break;
                        }
                        if (originalButton != null) break;
                    }
                }

                // Strategy 3: Fall back to first button with items
                if (originalButton == null || originalButtonItem == null)
                {
                    var allTabs = service.ServiceTemplate.ServiceTabs.ToArray();
                    foreach (var tab in allTabs)
                    {
                        var allButtons = tab.ServiceButtons.ToArray();
                        foreach (var button in allButtons)
                        {
                            if (button.ServiceButtonItems.Count > 0)
                            {
                                originalButton = button;
                                originalButtonItem = button.ServiceButtonItems[0];
                                break;
                            }
                        }
                        if (originalButton != null) break;
                    }
                }

                if (originalButton == null || originalButtonItem == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Could not find original service button or item. Service: {undoRecord.OriginalServiceName}, Button: {undoRecord.OriginalButtonName}, CID: {undoRecord.OriginalCID}";
                    return result;
                }

                // Load the original item
                ItemOperationResult loadResult = service.LoadServiceItem(originalButton, originalButtonItem, false);
                if (loadResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to reload original item: {loadResult.Message}";
                    return result;
                }

                Item restoredItem = loadResult.ReturnObject as Item;
                if (restoredItem == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to get restored item.";
                    return result;
                }

                // Step 3: Restore all properties
                var transferOptions = new ItemSwapOptions
                {
                    TransferPosition = true,
                    TransferDimensions = true,
                    TransferOptions = true,
                    TransferCustomData = true,
                    TransferBasicInfo = true,
                    TransferStatusSection = true,
                    TransferPriceList = true
                };

                result.TransferResult = ItemPropertyTransfer.TransferProperties(undoRecord, restoredItem, transferOptions);

                // Step 4: Add to job
                var addResult = Job.AddItem(restoredItem);
                if (addResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to add restored item to job: {addResult.Message}";
                    return result;
                }

                // Step 5: Position at original location using connector-based positioning
                if (undoRecord.OriginalPosition != null && undoRecord.OriginalPosition.HasValidPosition)
                {
                    PositionItemUsingConnectors(restoredItem, undoRecord.OriginalPosition);
                }

                // Step 6: Update view
                Autodesk.Fabrication.UI.UIApplication.UpdateView(new[] { restoredItem }.ToList());

                result.Success = true;
                result.NewItem = restoredItem;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error during undo: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Gets the service buttons available for the item's service.
        /// </summary>
        /// <param name="item">The item to get service buttons for.</param>
        /// <returns>Array of service tabs with their buttons.</returns>
        public ServiceTab[] GetServiceTabsForItem(Item item)
        {
            if (item?.Service?.ServiceTemplate?.ServiceTabs == null)
                return new ServiceTab[0];

            return item.Service.ServiceTemplate.ServiceTabs.ToArray();
        }

        /// <summary>
        /// Positions an item using connector endpoint data from the Fabrication API.
        /// This method uses the Fabrication API directly instead of AutoCAD transactions
        /// to avoid threading issues when called from WPF UI.
        /// Note: Items on designlines (nodes/fittings) may not be repositioned correctly
        /// as they are constrained by their connections to adjacent items.
        /// </summary>
        /// <param name="item">The item to position.</param>
        /// <param name="originalPosition">The original position data containing connector endpoints.</param>
        /// <returns>True if positioning was attempted, false if skipped.</returns>
        private bool PositionItemUsingConnectors(Item item, ItemPositionData originalPosition)
        {
            if (item == null || originalPosition == null || !originalPosition.HasValidPosition)
                return false;

            try
            {
                // Get the new item's current primary connector position
                if (item.Connectors == null || item.Connectors.Count == 0)
                    return false;

                Point3D currentConnectorPos = item.GetConnectorEndPoint(0);
                Point3D targetConnectorPos = originalPosition.PrimaryEndpoint;

                // Calculate the offset needed to move from current position to target position
                double offsetX = targetConnectorPos.X - currentConnectorPos.X;
                double offsetY = targetConnectorPos.Y - currentConnectorPos.Y;
                double offsetZ = targetConnectorPos.Z - currentConnectorPos.Z;

                // Only move if there's actually a difference (more than 0.1 units)
                if (Math.Abs(offsetX) < 0.1 && Math.Abs(offsetY) < 0.1 && Math.Abs(offsetZ) < 0.1)
                    return true; // Already in position

                // Use AutoCAD transformation to move the item
                MoveItemWithOffset(item, offsetX, offsetY, offsetZ);
                return true;
            }
            catch (Exception)
            {
                // Positioning failed - item will remain at default location
                // This is non-critical, so we don't propagate the error
                return false;
            }
        }

        /// <summary>
        /// Moves an item by the specified offset using AutoCAD commands.
        /// Uses SendStringToExecute to issue MOVE command which works better for Fabrication items.
        /// </summary>
        private void MoveItemWithOffset(Item item, double offsetX, double offsetY, double offsetZ)
        {
            try
            {
                // Get the AutoCAD handle for this item
                string handle = Job.GetACADHandleFromItem(item);
                if (string.IsNullOrEmpty(handle))
                    return;

                Document doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                // Try method 1: Use SendStringToExecute with MOVE command
                // This is more reliable for Fabrication items as it uses the native move functionality
                try
                {
                    // Format displacement with proper decimal separator for AutoCAD
                    string dispX = offsetX.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string dispY = offsetY.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string dispZ = offsetZ.ToString(System.Globalization.CultureInfo.InvariantCulture);

                    // Build the MOVE command: select by handle, then specify displacement
                    // (handent "handle") selects the entity by handle
                    string moveCmd = $"_.MOVE (handent \"{handle}\")  0,0,0 @{dispX},{dispY},{dispZ} ";

                    doc.SendStringToExecute(moveCmd, true, false, false);
                    return;
                }
                catch
                {
                    // Fall back to direct transformation if SendStringToExecute fails
                }

                // Method 2: Direct transformation using proper document context
                using (DocumentLock docLock = doc.LockDocument())
                {
                    Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;

                    using (Transaction trans = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            long ln = Int64.Parse(handle, System.Globalization.NumberStyles.HexNumber);
                            Handle hn = new Handle(ln);
                            ObjectId oid = db.GetObjectId(false, hn, 0);

                            if (!oid.IsNull && !oid.IsErased)
                            {
                                Entity entity = trans.GetObject(oid, OpenMode.ForWrite) as Entity;
                                if (entity != null)
                                {
                                    Vector3d displacement = new Vector3d(offsetX, offsetY, offsetZ);
                                    Matrix3d transform = Matrix3d.Displacement(displacement);
                                    entity.TransformBy(transform);
                                }
                            }
                        }
                        catch { }

                        trans.Commit();
                    }
                }
            }
            catch (Exception)
            {
                // Move failed - item stays at current position
            }
        }

        /// <summary>
        /// Gets the button name and item path for an item by matching CID/pattern number.
        /// Uses filename matching to avoid collection modification issues.
        /// </summary>
        private (string buttonName, string itemPath) FindOriginalButtonInfo(Item item)
        {
            if (item?.Service?.ServiceTemplate?.ServiceTabs == null)
                return (null, null);

            int itemCID = item.CID;
            string itemName = item.Name;

            // Convert to arrays to avoid collection modification during enumeration
            var tabs = item.Service.ServiceTemplate.ServiceTabs.ToArray();

            foreach (var tab in tabs)
            {
                var buttons = tab.ServiceButtons.ToArray();
                foreach (var button in buttons)
                {
                    var buttonItems = button.ServiceButtonItems.ToArray();
                    foreach (var buttonItem in buttonItems)
                    {
                        if (string.IsNullOrEmpty(buttonItem.ItemPath))
                            continue;

                        // Use filename matching instead of loading items (to avoid collection modification)
                        try
                        {
                            string fileName = System.IO.Path.GetFileNameWithoutExtension(buttonItem.ItemPath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                // Check if filename contains the CID number or matches item name
                                if (fileName.Contains(itemCID.ToString()) ||
                                    fileName.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                                    fileName.IndexOf(itemName, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    return (button.Name, buttonItem.ItemPath);
                                }
                            }
                        }
                        catch
                        {
                            // Skip this button item
                        }
                    }
                }
            }

            // Fallback: return first button with any items as a last resort
            foreach (var tab in tabs)
            {
                var buttons = tab.ServiceButtons.ToArray();
                foreach (var button in buttons)
                {
                    if (button.ServiceButtonItems.Count > 0)
                    {
                        var firstItem = button.ServiceButtonItems[0];
                        return (button.Name, firstItem.ItemPath);
                    }
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Gets the button name for an item.
        /// </summary>
        private string GetOriginalButtonName(Item item)
        {
            var (buttonName, _) = FindOriginalButtonInfo(item);
            return buttonName;
        }

        /// <summary>
        /// Gets the ITM path for an item.
        /// </summary>
        private string GetOriginalItemPath(Item item)
        {
            var (_, itemPath) = FindOriginalButtonInfo(item);
            return itemPath;
        }
    }
}
