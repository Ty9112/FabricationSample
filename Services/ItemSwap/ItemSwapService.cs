using System;
using System.Linq;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using Autodesk.Fabrication.Geometry;
using FabricationSample.Models;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

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

                // Store the original item's service button info
                undoRecord.OriginalButtonName = GetOriginalButtonName(originalItem);
                undoRecord.OriginalItemPath = GetOriginalItemPath(originalItem);

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

                // Step 4: Capture position BEFORE deleting original item
                if (options.TransferPosition && !string.IsNullOrEmpty(undoRecord.OriginalAcadHandle))
                {
                    PositionNewItemAtOriginalLocation(newItem, undoRecord.OriginalAcadHandle);
                }

                // Step 5: Delete the original item
                ItemOperationResult deleteResult = Job.DeleteItem(originalItem);
                if (deleteResult.Status != ResultStatus.Succeeded)
                {
                    // Try to clean up the new item
                    try { Job.DeleteItem(newItem); } catch { }

                    result.Success = false;
                    result.ErrorMessage = $"Failed to delete original item: {deleteResult.Message}";
                    return result;
                }

                // Step 6: Add the new item to the job
                ItemOperationResult addResult = Job.AddItem(newItem);
                if (addResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to add new item to job: {addResult.Message}";
                    return result;
                }

                // Step 7: Apply pending position to the new item (after it's added to job)
                if (options.TransferPosition && _hasPendingPosition)
                {
                    ApplyPendingPosition(newItem);
                }

                // Step 8: Update undo record with new item info and push to stack
                undoRecord.NewItemUniqueId = newItem.UniqueId;
                undoRecord.NewItemName = newItem.Name;
                undoRecord.Description = $"Swapped '{undoRecord.OriginalItemName}' with '{newItem.Name}'";
                _undoManager.RecordSwap(undoRecord);

                // Step 9: Update the view
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

                // Find the original button and item
                ServiceButton originalButton = null;
                ServiceButtonItem originalButtonItem = null;

                foreach (var tab in service.ServiceTemplate.ServiceTabs)
                {
                    foreach (var button in tab.ServiceButtons)
                    {
                        if (button.Name == undoRecord.OriginalButtonName)
                        {
                            originalButton = button;
                            // Try to find the item by path
                            for (int i = 0; i < button.ServiceButtonItems.Count; i++)
                            {
                                if (button.ServiceButtonItems[i].ItemPath == undoRecord.OriginalItemPath)
                                {
                                    originalButtonItem = button.ServiceButtonItems[i];
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    if (originalButton != null) break;
                }

                if (originalButton == null || originalButtonItem == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not find original service button or item.";
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

                // Step 4: Add to job first, then position
                // Step 5: Add to job
                var addResult = Job.AddItem(restoredItem);
                if (addResult.Status != ResultStatus.Succeeded)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to add restored item to job: {addResult.Message}";
                    return result;
                }

                // Step 6: Position at original location (after item is added)
                if (undoRecord.OriginalPosition != null)
                {
                    PositionItemAtLocation(restoredItem, undoRecord.OriginalPosition, undoRecord.OriginalAcadHandle);
                }

                // Step 7: Update view
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
        /// Positions an item at the specified location using AutoCAD transformation.
        /// </summary>
        private void PositionItemAtLocation(Item newItem, ItemPositionData originalPosition, string originalHandle = null)
        {
            if (originalPosition == null)
                return;

            try
            {
                // Get the AutoCAD handle of the new item after it's added to the job
                string newHandle = Job.GetACADHandleFromItem(newItem);
                if (string.IsNullOrEmpty(newHandle))
                    return;

                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;

                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // Get the new item's entity
                    long newLn = Int64.Parse(newHandle, System.Globalization.NumberStyles.HexNumber);
                    Handle newHn = new Handle(newLn);
                    ObjectId newId = db.GetObjectId(false, newHn, 0);

                    if (newId.IsNull || newId.IsErased)
                    {
                        trans.Commit();
                        return;
                    }

                    Entity newEntity = trans.GetObject(newId, OpenMode.ForWrite) as Entity;
                    if (newEntity == null)
                    {
                        trans.Commit();
                        return;
                    }

                    // Calculate the translation vector from new item's current position to original position
                    // Get the new item's connector position for reference
                    Point3D newConnectorPos = default;
                    bool hasNewConnector = false;
                    try
                    {
                        if (newItem.Connectors != null && newItem.Connectors.Count > 0)
                        {
                            newConnectorPos = newItem.GetConnectorEndPoint(0);
                            hasNewConnector = true;
                        }
                    }
                    catch { }

                    if (hasNewConnector && originalPosition.HasValidPosition)
                    {
                        // Create translation from current position to original position
                        Vector3d translation = new Vector3d(
                            originalPosition.PrimaryEndpoint.X - newConnectorPos.X,
                            originalPosition.PrimaryEndpoint.Y - newConnectorPos.Y,
                            originalPosition.PrimaryEndpoint.Z - newConnectorPos.Z
                        );

                        // Apply translation transformation
                        Matrix3d transformMatrix = Matrix3d.Displacement(translation);
                        newEntity.TransformBy(transformMatrix);
                    }

                    trans.Commit();
                }
            }
            catch (Exception)
            {
                // Positioning failed - item will remain at default location
                // This is non-critical, so we don't propagate the error
            }
        }

        /// <summary>
        /// Positions a new item at the same location as an original item using handle-based transformation.
        /// </summary>
        private void PositionNewItemAtOriginalLocation(Item newItem, string originalAcadHandle)
        {
            if (string.IsNullOrEmpty(originalAcadHandle))
                return;

            try
            {
                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;

                // Get original entity's position before it gets deleted
                Point3d originalCenter = default;
                bool hasOriginalCenter = false;

                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    long origLn = Int64.Parse(originalAcadHandle, System.Globalization.NumberStyles.HexNumber);
                    Handle origHn = new Handle(origLn);
                    ObjectId origId = db.GetObjectId(false, origHn, 0);

                    if (!origId.IsNull && !origId.IsErased)
                    {
                        Entity origEntity = trans.GetObject(origId, OpenMode.ForRead) as Entity;
                        if (origEntity != null)
                        {
                            // Get the geometric center of the original entity
                            Extents3d extents = origEntity.GeometricExtents;
                            originalCenter = new Point3d(
                                (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                                (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                                (extents.MinPoint.Z + extents.MaxPoint.Z) / 2
                            );
                            hasOriginalCenter = true;
                        }
                    }
                    trans.Commit();
                }

                // Store this for later positioning after the new item is added
                if (hasOriginalCenter)
                {
                    _pendingPositionCenter = originalCenter;
                    _hasPendingPosition = true;
                }
            }
            catch (Exception)
            {
                // Position capture failed
            }
        }

        // Temporary storage for positioning
        private Point3d _pendingPositionCenter;
        private bool _hasPendingPosition;

        /// <summary>
        /// Applies pending position to a newly added item.
        /// </summary>
        private void ApplyPendingPosition(Item newItem)
        {
            if (!_hasPendingPosition)
                return;

            try
            {
                string newHandle = Job.GetACADHandleFromItem(newItem);
                if (string.IsNullOrEmpty(newHandle))
                    return;

                Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                Autodesk.AutoCAD.DatabaseServices.Database db = doc.Database;

                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    long newLn = Int64.Parse(newHandle, System.Globalization.NumberStyles.HexNumber);
                    Handle newHn = new Handle(newLn);
                    ObjectId newId = db.GetObjectId(false, newHn, 0);

                    if (newId.IsNull || newId.IsErased)
                    {
                        trans.Commit();
                        return;
                    }

                    Entity newEntity = trans.GetObject(newId, OpenMode.ForWrite) as Entity;
                    if (newEntity == null)
                    {
                        trans.Commit();
                        return;
                    }

                    // Get new entity's current center
                    try
                    {
                        Extents3d newExtents = newEntity.GeometricExtents;
                        Point3d newCenter = new Point3d(
                            (newExtents.MinPoint.X + newExtents.MaxPoint.X) / 2,
                            (newExtents.MinPoint.Y + newExtents.MaxPoint.Y) / 2,
                            (newExtents.MinPoint.Z + newExtents.MaxPoint.Z) / 2
                        );

                        // Calculate translation
                        Vector3d translation = _pendingPositionCenter - newCenter;

                        // Apply transformation
                        Matrix3d transformMatrix = Matrix3d.Displacement(translation);
                        newEntity.TransformBy(transformMatrix);
                    }
                    catch { }

                    trans.Commit();
                }
            }
            catch (Exception)
            {
                // Positioning failed
            }
            finally
            {
                _hasPendingPosition = false;
            }
        }

        /// <summary>
        /// Gets the button name for an item.
        /// </summary>
        private string GetOriginalButtonName(Item item)
        {
            // Try to find the button this item came from
            if (item?.Service?.ServiceTemplate?.ServiceTabs == null)
                return null;

            foreach (var tab in item.Service.ServiceTemplate.ServiceTabs)
            {
                foreach (var button in tab.ServiceButtons)
                {
                    foreach (var buttonItem in button.ServiceButtonItems)
                    {
                        // Match by database ID or pattern number
                        if (buttonItem.ItemPath != null &&
                            item.SourceDescription != null &&
                            item.SourceDescription.Contains(button.Name))
                        {
                            return button.Name;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the ITM path for an item.
        /// </summary>
        private string GetOriginalItemPath(Item item)
        {
            // The item path would need to be determined from the service button item
            // This is a best-effort approach
            if (item?.Service?.ServiceTemplate?.ServiceTabs == null)
                return null;

            foreach (var tab in item.Service.ServiceTemplate.ServiceTabs)
            {
                foreach (var button in tab.ServiceButtons)
                {
                    foreach (var buttonItem in button.ServiceButtonItems)
                    {
                        // Try to match by pattern number or name
                        if (buttonItem.ItemPath != null)
                        {
                            // This is approximate - would need refinement
                            return buttonItem.ItemPath;
                        }
                    }
                }
            }

            return null;
        }
    }
}
