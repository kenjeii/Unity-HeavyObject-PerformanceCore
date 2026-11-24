using Unity.Profiling;
using UnityEngine;

namespace Kenjeii.UnityPerfCore.ObjectPooling
{
    /// <summary>
    /// Zero-GC typed object pool. Renting and returning never call Instantiate or
    /// Destroy after pre-warming, and allocate nothing on the hot path.
    ///
    /// Uses a stack of pre-created instances plus a HashSet only when collection
    /// checking is enabled. Disable collection checking in production for maximum
    /// throughput (double-release is then the caller's responsibility).
    /// </summary>
    /// <typeparam name="T">Pooled type. Must implement IPoolable.</typeparam>
    public sealed class TypedObjectPool<T> : IPoolReturner where T : class, IPoolable
    {
        private static readonly ProfilerMarker RentMarker =
            new ProfilerMarker("PerfCore.Pool.Rent");
        private static readonly ProfilerMarker ReturnMarker =
            new ProfilerMarker("PerfCore.Pool.Return");
        private static readonly ProfilerMarker PrewarmMarker =
            new ProfilerMarker("PerfCore.Pool.Prewarm");

        private readonly T[] _pool;
        private readonly Factory _factory;
        private readonly bool _collectionCheck;
        private int _count;
        private int _rented;

        /// <summary>Delegate that creates one fresh instance. Called only during pre-warm or expansion.</summary>
        public delegate T Factory();

        /// <summary>Current number of available instances.</summary>
        public int Available => _count;

        /// <summary>Current number of rented instances.</summary>
        public int Rented => _rented;

        /// <summary>Hard capacity. Renting beyond this returns null.</summary>
        public int Capacity => _pool.Length;

        /// <summary>Creates a pool with a fixed capacity.</summary>
        public TypedObjectPool(Factory factory, int capacity, bool collectionCheck = false)
        {
            _factory = factory;
            _pool = new T[capacity < 1 ? 1 : capacity];
            _collectionCheck = collectionCheck;
            _count = 0;
            _rented = 0;
        }

        /// <summary>Pre-creates instances so no allocation happens at runtime.</summary>
        public void Prewarm(int amount)
        {
            using (PrewarmMarker.Auto())
            {
                int target = Mathf.Min(amount, _pool.Length);
                for (int i = _count; i < target; i++)
                {
                    T instance = _factory();
                    if (instance == null) break;
                    MonoPoolable mono = instance as MonoPoolable;
                    if (mono != null) mono.SetReturner(this);
                    _pool[i] = instance;
                }
                if (target > _count) _count = target;
            }
        }

        /// <summary>Rents one instance. Returns null if the pool is exhausted.</summary>
        public T Rent()
        {
            using (RentMarker.Auto())
            {
                if (_count == 0) return null;
                _count--;
                T item = _pool[_count];
                _pool[_count] = null;
                _rented++;
                item.OnRent();
                return item;
            }
        }

        /// <summary>Returns one instance. Silently ignores null or foreign objects.</summary>
        public void Return(T item)
        {
            using (ReturnMarker.Auto())
            {
                if (item == null) return;
                if (_collectionCheck && _count > 0)
                {
                    for (int i = 0; i < _count; i++)
                    {
                        if (ReferenceEquals(_pool[i], item)) return;
                    }
                }
                if (_count >= _pool.Length) return;

                item.OnReturn();
                _pool[_count++] = item;
                if (_rented > 0) _rented--;
            }
        }

        /// <inheritdoc />
        public void Return(IPoolable item) => Return(item as T);

        /// <summary>Clears all available instances. Rented instances are not affected.</summary>
        public void ClearAvailable()
        {
            for (int i = 0; i < _count; i++) _pool[i] = null;
            _count = 0;
        }
    }
}
