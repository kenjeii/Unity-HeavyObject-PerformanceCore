using System.Collections.Generic;

namespace Kenjeii.UnityPerfCore.CentralizedUpdate
{
    /// <summary>
    /// Allocation-free registry of tickable objects. Uses swap-back removal so
    /// unregistering during a tick never shifts the whole list or allocates.
    /// Add/remove requests made mid-tick are deferred and applied at the end of
    /// the dispatch, which keeps iteration stable without copying the list.
    /// </summary>
    /// <typeparam name="T">Tickable contract interface.</typeparam>
    public sealed class TickRegistry<T> where T : class
    {
        private readonly List<T> _items = new List<T>(256);
        private readonly List<T> _pendingAdd = new List<T>(32);
        private readonly List<T> _pendingRemove = new List<T>(32);
        private readonly Dictionary<T, int> _index = new Dictionary<T, int>(256);

        private bool _dispatching;

        /// <summary>Number of registered items, excluding deferred changes.</summary>
        public int Count => _items.Count;

        /// <summary>Registers an item. Safe to call during dispatch.</summary>
        public void Register(T item)
        {
            if (item == null || _index.ContainsKey(item)) return;
            if (_dispatching)
            {
                _pendingAdd.Add(item);
                return;
            }
            Insert(item);
        }

        /// <summary>Unregisters an item. Safe to call during dispatch.</summary>
        public void Unregister(T item)
        {
            if (item == null || !_index.ContainsKey(item)) return;
            if (_dispatching)
            {
                _pendingRemove.Add(item);
                return;
            }
            RemoveNow(item);
        }

        /// <summary>
        /// Dispatches to every registered item. Items registering or unregistering
        /// during the loop are applied afterwards, so the iteration is never invalidated.
        /// </summary>
        /// <typeparam name="TAction">A struct implementing the dispatch action.</typeparam>
        public void Dispatch<TAction>(ref TAction action) where TAction : struct, IDispatchAction<T>
        {
            _dispatching = true;
            for (int i = 0; i < _items.Count; i++)
            {
                action.Invoke(_items[i]);
            }
            _dispatching = false;

            // Apply deferred changes. No temporary lists, no LINQ, no closures.
            for (int i = 0; i < _pendingRemove.Count; i++) RemoveNow(_pendingRemove[i]);
            for (int i = 0; i < _pendingAdd.Count; i++) Insert(_pendingAdd[i]);
            _pendingRemove.Clear();
            _pendingAdd.Clear();
        }

        /// <summary>Removes everything. Reuses allocated capacity.</summary>
        public void Clear()
        {
            _items.Clear();
            _index.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        private void Insert(T item)
        {
            _index[item] = _items.Count;
            _items.Add(item);
        }

        private void RemoveNow(T item)
        {
            int index;
            if (!_index.TryGetValue(item, out index)) return;

            int last = _items.Count - 1;
            if (index != last)
            {
                // Swap-back: O(1) removal, no shifting, no allocation.
                T moved = _items[last];
                _items[index] = moved;
                _index[moved] = index;
            }
            _items.RemoveAt(last);
            _index.Remove(item);
        }
    }

    /// <summary>
    /// Struct-based dispatch action. Implementations are structs so the JIT can inline
    /// the call and no delegate or closure is allocated per frame.
    /// </summary>
    public interface IDispatchAction<in T>
    {
        /// <summary>Applies one unit of work to an item.</summary>
        void Invoke(T item);
    }
}
