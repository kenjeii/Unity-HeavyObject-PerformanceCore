using System.Collections.Generic;
using UnityEngine;

namespace Kenjeii.UnityPerfCore.ObjectPooling
{
    /// <summary>
    /// Caches component lookups once so hot paths never call GetComponent.
    /// Uses flat arrays for both requested types and resolved components — no
    /// dictionary iteration, no LINQ, no allocation on the read path.
    /// </summary>
    public sealed class ComponentCache
    {
        private readonly System.Type[] _types = new System.Type[8];
        private readonly Component[] _components = new Component[8];
        private int _count;

        /// <summary>Number of distinct component types cached.</summary>
        public int Count => _count;

        /// <summary>
        /// Scans the host once and caches one component per requested type.
        /// Call on rent or initialization — never in a tick loop.
        /// </summary>
        public void Cache(GameObject host, System.Type[] requestedTypes, List<Component> scratchBuffer)
        {
            _count = 0;
            int slots = requestedTypes.Length < _types.Length ? requestedTypes.Length : _types.Length;
            for (int i = 0; i < slots; i++)
            {
                _types[i] = requestedTypes[i];
                _components[i] = null;
            }
            _count = slots;

            if (host == null) return;
            scratchBuffer.Clear();
            host.GetComponents(scratchBuffer);

            for (int i = 0; i < scratchBuffer.Count; i++)
            {
                Component candidate = scratchBuffer[i];
                System.Type candidateType = candidate.GetType();
                for (int slot = 0; slot < _count; slot++)
                {
                    if (_components[slot] != null) continue;
                    if (_types[slot] != null && _types[slot].IsAssignableFrom(candidateType))
                    {
                        _components[slot] = candidate;
                    }
                }
            }
        }

        /// <summary>Gets a cached component by exact type. Zero allocation.</summary>
        public T Get<T>() where T : class
        {
            System.Type type = typeof(T);
            for (int i = 0; i < _count; i++)
            {
                if (_types[i] == type) return _components[i] as T;
            }
            return null;
        }

        /// <summary>Clears all cached references without shrinking arrays.</summary>
        public void Reset()
        {
            for (int i = 0; i < _count; i++)
            {
                _types[i] = null;
                _components[i] = null;
            }
            _count = 0;
        }
    }
}
