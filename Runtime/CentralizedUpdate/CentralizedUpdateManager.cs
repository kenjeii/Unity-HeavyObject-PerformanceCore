using Unity.Profiling;
using UnityEngine;

namespace Kenjeii.UnityPerfCore.CentralizedUpdate
{
    /// <summary>
    /// Single MonoBehaviour that drives every registered ITickable / ILateTickable /
    /// IFixedTickable. Replaces thousands of per-object Update() calls with three
    /// native-to-managed crossings per frame.
    ///
    /// Heavy simulation objects can additionally be placed in time-sliced buckets so
    /// only one bucket is ticked per frame, spreading CPU cost across frames.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class CentralizedUpdateManager : MonoBehaviour
    {
        private struct TickAction : IDispatchAction<ITickable>
        {
            public float DeltaTime;
            public void Invoke(ITickable item) => item.Tick(DeltaTime);
        }

        private struct LateTickAction : IDispatchAction<ILateTickable>
        {
            public float DeltaTime;
            public void Invoke(ILateTickable item) => item.LateTick(DeltaTime);
        }

        private struct FixedTickAction : IDispatchAction<IFixedTickable>
        {
            public float DeltaTime;
            public void Invoke(IFixedTickable item) => item.FixedTick(DeltaTime);
        }

        private static readonly ProfilerMarker TickMarker =
            new ProfilerMarker("PerfCore.Tick");
        private static readonly ProfilerMarker LateTickMarker =
            new ProfilerMarker("PerfCore.LateTick");
        private static readonly ProfilerMarker FixedTickMarker =
            new ProfilerMarker("PerfCore.FixedTick");
        private static readonly ProfilerMarker SlicedTickMarker =
            new ProfilerMarker("PerfCore.SlicedTick");

        private readonly TickRegistry<ITickable> _ticks = new TickRegistry<ITickable>();
        private readonly TickRegistry<ILateTickable> _lateTicks = new TickRegistry<ILateTickable>();
        private readonly TickRegistry<IFixedTickable> _fixedTicks = new TickRegistry<IFixedTickable>();
        private readonly TickRegistry<ITickable>[] _buckets = new TickRegistry<ITickable>[4];
        private int _activeBucket;

        /// <summary>Singleton instance. Created on demand.</summary>
        public static CentralizedUpdateManager Instance { get; private set; }

        /// <summary>Number of time-sliced buckets. Must be set before registering sliced items.</summary>
        public int BucketCount => _buckets.Length;

        /// <summary>Registered tickable count (non-sliced).</summary>
        public int TickableCount => _ticks.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new TickRegistry<ITickable>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Registers an object for the normal per-frame tick.</summary>
        public void Register(ITickable item) => _ticks.Register(item);

        /// <summary>Unregisters an object from the normal per-frame tick.</summary>
        public void Unregister(ITickable item) => _ticks.Unregister(item);

        /// <summary>Registers an object for the late tick.</summary>
        public void RegisterLate(ILateTickable item) => _lateTicks.Register(item);

        /// <summary>Unregisters an object from the late tick.</summary>
        public void UnregisterLate(ILateTickable item) => _lateTicks.Unregister(item);

        /// <summary>Registers an object for the fixed physics tick.</summary>
        public void RegisterFixed(IFixedTickable item) => _fixedTicks.Register(item);

        /// <summary>Unregisters an object from the fixed physics tick.</summary>
        public void UnregisterFixed(IFixedTickable item) => _fixedTicks.Unregister(item);

        /// <summary>
        /// Registers a heavy object into a time-sliced bucket. Only one bucket is
        /// dispatched per frame, so the caller pays 1/N of the total cost per frame.
        /// </summary>
        public void RegisterSliced(ITickable item, int bucket)
        {
            if (bucket < 0 || bucket >= _buckets.Length) bucket = 0;
            _buckets[bucket].Register(item);
        }

        /// <summary>Unregisters a heavy object from its time-sliced bucket.</summary>
        public void UnregisterSliced(ITickable item, int bucket)
        {
            if (bucket < 0 || bucket >= _buckets.Length) bucket = 0;
            _buckets[bucket].Unregister(item);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            TickAction tick = new TickAction { DeltaTime = dt };
            using (TickMarker.Auto()) _ticks.Dispatch(ref tick);

            // One slice per frame: amortized cost for heavy simulation objects.
            SlicedTickAction sliced = new SlicedTickAction { DeltaTime = dt * _buckets.Length };
            using (SlicedTickMarker.Auto()) _buckets[_activeBucket].Dispatch(ref sliced);
            _activeBucket = (_activeBucket + 1) % _buckets.Length;
        }

        private void LateUpdate()
        {
            LateTickAction action = new LateTickAction { DeltaTime = Time.deltaTime };
            using (LateTickMarker.Auto()) _lateTicks.Dispatch(ref action);
        }

        private void FixedUpdate()
        {
            FixedTickAction action = new FixedTickAction { DeltaTime = Time.fixedDeltaTime };
            using (FixedTickMarker.Auto()) _fixedTicks.Dispatch(ref action);
        }

        private struct SlicedTickAction : IDispatchAction<ITickable>
        {
            public float DeltaTime;
            public void Invoke(ITickable item) => item.Tick(DeltaTime);
        }
    }
}
