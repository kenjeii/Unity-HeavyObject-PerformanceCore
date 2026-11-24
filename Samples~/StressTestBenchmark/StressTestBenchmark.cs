using System.Collections.Generic;
using Kenjeii.UnityPerfCore.CentralizedUpdate;
using Kenjeii.UnityPerfCore.FlyweightData;
using Kenjeii.UnityPerfCore.ObjectPooling;
using Kenjeii.UnityPerfCore.TimeSlicing;
using Unity.Profiling;
using UnityEngine;

namespace Kenjeii.UnityPerfCore.Benchmark
{
    /// <summary>
    /// Self-contained stress test comparing a naive per-object Update + Instantiate/Destroy
    /// workload against the optimized architecture (centralized tick + pool + flyweight
    /// config + throttled emitters + time-sliced heavy work).
    ///
    /// Requires zero external assets. Attach to an empty GameObject, choose a mode,
    /// set object count, press Play, and read GC Alloc / Frame Time in the Profiler.
    /// </summary>
    public sealed class StressTestBenchmark : MonoBehaviour
    {
        /// <summary>Which architecture to exercise.</summary>
        public enum BenchmarkMode
        {
            /// <summary>N objects each with their own Update() and spawn/destroy churn.</summary>
            Naive,

            /// <summary>N objects driven by CentralizedUpdateManager + ObjectPool + Flyweight.</summary>
            Optimized
        }

        private sealed class HeavyConfig : FlyweightConfig
        {
            public readonly float Speed;
            public readonly int Weight;

            public HeavyConfig(string id, float speed, int weight) : base(id)
            {
                Speed = speed;
                Weight = weight;
            }
        }

        /// <summary>
        /// Naive object: its own MonoBehaviour.Update and periodic Destroy/Instantiate.
        /// Intentionally allocates to show the GC cost.
        /// </summary>
        private sealed class NaiveWorker : MonoBehaviour, ITickable
        {
            private float _speed;
            private float _phase;
            private float _timer;

            public void Configure(float speed, float phase)
            {
                _speed = speed;
                _phase = phase;
            }

            private void Update()
            {
                // Deliberate per-frame allocation to expose GC pressure.
                string label = "naive_" + _phase.ToString("F2");
                _phase += Time.deltaTime * _speed;
                _timer += Time.deltaTime;
                if (_timer > 2f)
                {
                    _timer = 0f;
                    // Churn: destroy and recreate a child GameObject periodically.
                    GameObject child = new GameObject(label);
                    Destroy(child, 0.5f);
                }
            }

            public void Tick(float deltaTime) { }
        }

        /// <summary>
        /// Optimized object: no Update method. Holds only a flyweight state struct and
        /// is driven by the centralized manager. All heavy data is shared config.
        /// </summary>
        private sealed class OptimizedWorker : MonoPoolable, ITickable
        {
            private FlyweightState<HeavyConfig> _state;
            private Transform _cachedTransform;

            public void Configure(HeavyConfig config)
            {
                _state.Initialize(config);
                _cachedTransform = transform; // cached once, never GetComponent in tick
            }

            public void Tick(float deltaTime)
            {
                if (!_state.Active) return;
                _state.Age += deltaTime;
                // Cheap math only: no allocation, no GetComponent, no string work.
                float angle = _state.Age * _state.Config.Speed * 57.29578f;
                _cachedTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            public override void OnReturn()
            {
                base.OnReturn();
                _state.Active = false;
            }
        }

        private static readonly ProfilerMarker NaiveMarker =
            new ProfilerMarker("Benchmark.Naive");
        private static readonly ProfilerMarker OptimizedMarker =
            new ProfilerMarker("Benchmark.Optimized");

        [Header("Benchmark settings")]
        [SerializeField] private BenchmarkMode _mode = BenchmarkMode.Optimized;

        [Range(100, 20000)]
        [SerializeField] private int _objectCount = 5000;

        [SerializeField] private float _emitterCooldown = 0.2f;
        [SerializeField] private int _maxEmitters = 10;

        private readonly List<NaiveWorker> _naiveWorkers = new List<NaiveWorker>();
        private readonly List<OptimizedWorker> _pooledWorkers = new List<OptimizedWorker>();
        private CentralizedUpdateManager _manager;
        private TypedObjectPool<OptimizedWorker> _pool;
        private EmitterThrottler _throttler;
        private HeavyConfig _sharedConfig;
        private bool _initialized;

        /// <summary>Current mode. Changing it at runtime requires calling Reset().</summary>
        public BenchmarkMode Mode => _mode;

        /// <summary>Current object count.</summary>
        public int ObjectCount => _objectCount;

        private void Start()
        {
            Initialize();
        }

        /// <summary>Rebuilds the benchmark with current settings.</summary>
        public void Rebuild()
        {
            Teardown();
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _sharedConfig = new HeavyConfig("stress-worker", 1.5f, 8);
            _throttler = new EmitterThrottler(_maxEmitters);

            if (_mode == BenchmarkMode.Naive)
            {
                using (NaiveMarker.Auto())
                {
                    for (int i = 0; i < _objectCount; i++)
                    {
                        GameObject go = new GameObject("NaiveWorker_" + i);
                        NaiveWorker worker = go.AddComponent<NaiveWorker>();
                        worker.Configure(1f + (i % 10) * 0.1f, i * 0.01f);
                        _naiveWorkers.Add(worker);
                    }
                }
            }
            else
            {
                using (OptimizedMarker.Auto())
                {
                    _manager = gameObject.GetComponent<CentralizedUpdateManager>();
                    if (_manager == null) _manager = gameObject.AddComponent<CentralizedUpdateManager>();

                    _pool = new TypedObjectPool<OptimizedWorker>(
                        CreateOptimizedWorker,
                        _objectCount,
                        collectionCheck: false);
                    _pool.Prewarm(_objectCount);

                    for (int i = 0; i < _objectCount; i++)
                    {
                        OptimizedWorker worker = _pool.Rent();
                        if (worker == null) break;
                        worker.Configure(_sharedConfig);
                        _manager.Register(worker);
                        _pooledWorkers.Add(worker);
                    }
                }
            }
        }

        private OptimizedWorker CreateOptimizedWorker()
        {
            GameObject go = new GameObject("OptimizedWorker");
            go.SetActive(false);
            return go.AddComponent<OptimizedWorker>();
        }

        private void Update()
        {
            if (!_initialized || _mode != BenchmarkMode.Optimized) return;

            // Demonstrate throttled emitter budget under high object count.
            for (int i = 0; i < _pooledWorkers.Count; i += 100)
            {
                if (_throttler.TryEmit(_emitterCooldown)) break;
            }
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Teardown()
        {
            if (_manager != null)
            {
                for (int i = 0; i < _pooledWorkers.Count; i++)
                {
                    _manager.Unregister(_pooledWorkers[i]);
                }
            }
            _pooledWorkers.Clear();

            for (int i = 0; i < _naiveWorkers.Count; i++)
            {
                if (_naiveWorkers[i] != null) Destroy(_naiveWorkers[i].gameObject);
            }
            _naiveWorkers.Clear();

            _pool?.ClearAvailable();
            _pool = null;
            _manager = null;
            _initialized = false;
        }
    }
}
