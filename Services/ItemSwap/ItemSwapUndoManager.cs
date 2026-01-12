using System;
using System.Collections.Generic;
using FabricationSample.Models;

namespace FabricationSample.Services.ItemSwap
{
    /// <summary>
    /// Manages the undo stack for item swap operations.
    /// Provides the ability to revert item swaps.
    /// </summary>
    public class ItemSwapUndoManager
    {
        private static ItemSwapUndoManager _instance;
        private static readonly object _lock = new object();

        private readonly Stack<ItemSwapUndoRecord> _undoStack;
        private const int MaxUndoLevels = 10;

        /// <summary>
        /// Gets the singleton instance of the undo manager.
        /// </summary>
        public static ItemSwapUndoManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ItemSwapUndoManager();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Event raised when the undo stack changes.
        /// </summary>
        public event EventHandler UndoStackChanged;

        /// <summary>
        /// Private constructor for singleton pattern.
        /// </summary>
        private ItemSwapUndoManager()
        {
            _undoStack = new Stack<ItemSwapUndoRecord>();
        }

        /// <summary>
        /// Gets whether there are any undo operations available.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets the number of undo operations available.
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Gets the description of the next undo operation.
        /// </summary>
        public string NextUndoDescription
        {
            get
            {
                if (_undoStack.Count == 0)
                    return null;
                return _undoStack.Peek().Description;
            }
        }

        /// <summary>
        /// Records a swap operation for potential undo.
        /// </summary>
        /// <param name="record">The undo record to push onto the stack.</param>
        public void RecordSwap(ItemSwapUndoRecord record)
        {
            if (record == null)
                return;

            lock (_lock)
            {
                // Maintain max undo levels
                while (_undoStack.Count >= MaxUndoLevels)
                {
                    // Remove oldest (we need to convert to list to remove from bottom)
                    var tempList = new List<ItemSwapUndoRecord>(_undoStack);
                    tempList.RemoveAt(tempList.Count - 1);
                    _undoStack.Clear();
                    for (int i = tempList.Count - 1; i >= 0; i--)
                        _undoStack.Push(tempList[i]);
                }

                _undoStack.Push(record);
            }

            OnUndoStackChanged();
        }

        /// <summary>
        /// Pops the most recent undo record from the stack.
        /// </summary>
        /// <returns>The most recent undo record, or null if stack is empty.</returns>
        public ItemSwapUndoRecord PopUndo()
        {
            lock (_lock)
            {
                if (_undoStack.Count == 0)
                    return null;

                var record = _undoStack.Pop();
                OnUndoStackChanged();
                return record;
            }
        }

        /// <summary>
        /// Peeks at the most recent undo record without removing it.
        /// </summary>
        /// <returns>The most recent undo record, or null if stack is empty.</returns>
        public ItemSwapUndoRecord PeekUndo()
        {
            lock (_lock)
            {
                if (_undoStack.Count == 0)
                    return null;
                return _undoStack.Peek();
            }
        }

        /// <summary>
        /// Clears all undo records.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _undoStack.Clear();
            }
            OnUndoStackChanged();
        }

        /// <summary>
        /// Gets all undo records (for display purposes).
        /// </summary>
        /// <returns>List of undo records, most recent first.</returns>
        public IReadOnlyList<ItemSwapUndoRecord> GetUndoHistory()
        {
            lock (_lock)
            {
                return new List<ItemSwapUndoRecord>(_undoStack).AsReadOnly();
            }
        }

        /// <summary>
        /// Raises the UndoStackChanged event.
        /// </summary>
        protected virtual void OnUndoStackChanged()
        {
            UndoStackChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
