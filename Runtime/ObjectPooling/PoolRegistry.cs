using System.Collections.Generic;

namespace Kenjeii.UnityPerfCore.ObjectPooling
{
    /// <summary>
    /// Central registry of typed pools keyed by an integer pool id. Callers resolve a
    /// pool once at initialization and keep the reference — no lookups on the hot path.
    /// The id-based Release is provided for objects that only know their pool id.
    /// </summary>
    public sealed class PoolRegistry
    {
        private readonly Dictionary<int, IRelease> _releasers = new Dictionary<int, IRelease>();
        private readonly Dictionary<int, object> _pools = new Dictionary<int, object>();

        private interface IRelease
        {
            void Release(object handle);
        }

        private sealed class Releaser<T> : IRelease where T : class, IPoolable
        {
            private readonly TypedObjectPool<T> _pool;
            public Releaser(TypedObjectPool<T> pool) { _pool = pool; }
            public void Release(object handle) => _pool.Return(handle as T);
        }

        /// <summary>Registers a typed pool under an id. Replaces any previous entry.</summary>
        public void Register<T>(int poolId, TypedObjectPool<T> pool) where T : class, IPoolable
        {
            if (pool == null) return;
            _pools[poolId] = pool;
            _releasers[poolId] = new Releaser<T>(pool);
        }

        /// <summary>Gets a registered typed pool, or null. Resolve once, not per frame.</summary>
        public TypedObjectPool<T> Get<T>(int poolId) where T : class, IPoolable
        {
            object found;
            return _pools.TryGetValue(poolId, out found) ? found as TypedObjectPool<T> : null;
        }

        /// <summary>Releases a handle by pool id.</summary>
        public void Release(int poolId, object handle)
        {
            IRelease releaser;
            if (_releasers.TryGetValue(poolId, out releaser)) releaser.Release(handle);
        }

        /// <summary>Removes all registered pools.</summary>
        public void Clear()
        {
            _pools.Clear();
            _releasers.Clear();
        }
    }
}
