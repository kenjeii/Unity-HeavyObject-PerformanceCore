# Unity Heavy Object Performance Core

A Unity UPM package demonstrating how to manage thousands of heavy computational objects without frame drops or GC spikes. The patterns are extracted from **Theseus' Cycle** (a 2D factory-automation game on Steam) and rewritten as a standalone, dependency-light engineering showcase.

The focus is exclusively on **object lifecycle, execution scheduling and memory architecture** — no art, shaders, scenes or game-specific mechanics.

Unity 2021.3+. Runtime uses only `System.*`, `UnityEngine.*` and `Unity.Profiling`.

## The problem

Unity's default component model does not scale to high object counts:

- **Native-to-managed bridge cost.** Each `MonoBehaviour.Update()` is a separate native→managed call. 5,000 objects means 5,000 crossings per frame before any real work happens.
- **GC spikes from churn.** Runtime `Instantiate` / `Destroy` allocates managed wrappers and native resources. Repeated spawning produces a sawtooth GC allocation curve and eventual collection hitches.
- **Redundant lookups.** `GetComponent` and `FindObjectOfType` inside tick methods repeat the same work every frame across the whole population.
- **Animator overhead.** An `Animator` per object carries an `AnimatorController` and `PlayableGraph` evaluation cost even for a simple sprite loop.
- **CPU cache misses.** Per-object `Update` methods scatter state across the heap; centralized, array-backed registries keep hot data contiguous.

## Architecture breakdown

### 1. Centralized update

`CentralizedUpdateManager` is a single `MonoBehaviour` that drives three registries (`ITickable`, `ILateTickable`, `IFixedTickable`). Three native→managed crossings replace thousands. `TickRegistry<T>` uses **swap-back removal** — unregistering an item moves the last element into its slot, so no list shifting and no allocation. Register/unregister during a tick is deferred to the end of dispatch via preallocated pending lists.

```csharp
public sealed class Enemy : MonoBehaviour, ITickable
{
    public void Tick(float dt) { /* AI step, no Update() method */ }

    private void OnEnable() => CentralizedUpdateManager.Instance.Register(this);
    private void OnDisable() => CentralizedUpdateManager.Instance.Unregister(this);
}
```

Dispatch uses a **struct action** so no delegate or closure is allocated per frame:

```csharp
private struct TickAction : IDispatchAction<ITickable>
{
    public float DeltaTime;
    public void Invoke(ITickable item) => item.Tick(DeltaTime);
}
```

### 2. Zero-GC object pooling

`TypedObjectPool<T>` uses a fixed-size array stack. `Prewarm(n)` creates all instances up front; rent and return after that are pure array operations with zero heap allocation. `IPoolable.OnRent` / `OnReturn` reset state instead of reconstructing objects. `MonoPoolable` lets a pooled `MonoBehaviour` call `Release()` on itself without holding a concrete pool reference.

```csharp
var pool = new TypedObjectPool<Bullet>(factory, capacity: 1000);
pool.Prewarm(1000);

Bullet b = pool.Rent();   // no allocation, no Instantiate
b.Configure(direction, speed);
b.Release();              // no Destroy — returns to pool
```

`PoolRegistry` stores pools by integer id so objects that only know their pool id can still return themselves.

### 3. Flyweight state separation

`FlyweightConfig` holds immutable shared data (one instance per kind). `FlyweightState<TConfig>` is a lightweight struct holding only a config reference plus mutable per-instance values (`Age`, `Active`). Thousands of objects share one config; per-instance memory stays tiny.

```csharp
public sealed class EnemyConfig : FlyweightConfig
{
    public readonly float Speed;
    public readonly int Health;
    public EnemyConfig(float speed, int health) : base("enemy") { Speed = speed; Health = health; }
}

FlyweightState<EnemyConfig> state;
state.Initialize(sharedConfig);  // no copy of shared data
```

`ComponentCache` complements this by resolving components **once** into flat arrays — hot paths call `cache.Get<T>()` instead of `GetComponent<T>()`.

### 4. Time-slicing, throttling and dirty flags

- **`FrameBudgetScheduler`** processes `ITimeSlicedTask` items under a strict per-frame realtime budget (default 12 ms), checking the clock after each step and using swap-back removal when a task completes.
- **`EmitterThrottler`** caps simultaneous sounds/VFX with a fixed-size ring of end timestamps — no growing lists, no coroutines.
- **`DirtyFlag`** is a bitmask so a deferred pass evaluates only objects whose state changed, instead of the full population.
- **`CentralizedUpdateManager.RegisterSliced`** places heavy objects in one of four buckets; only one bucket ticks per frame, spreading cost across frames.

```csharp
var scheduler = new FrameBudgetScheduler { FrameBudgetSeconds = 0.012f };
scheduler.Enqueue(myHeavyLoadTask);
scheduler.ProcessOneFrame();  // call from Update or a coroutine
```

### 5. Animator bypass

`SpriteFrameClip` holds shared immutable frames and per-frame durations. `SpriteFrameState` is a per-instance struct with just an index and timer. `SpriteFrameAnimator.Advance` steps frames in C# using caller-supplied `deltaTime`, with trigger-frame support and no `Animator` component.

## Performance results

> Placeholder table — run the included benchmark and replace the bracketed values with your Profiler readings.

| Architecture | Object count | Frame time (ms) | GC alloc / frame | Native Update calls |
|---|---|---|---|---|
| Naive baseline | 5,000 | `[INSERT_PROFILER_METRIC]` | `[INSERT_PROFILER_METRIC]` | 5,000 |
| Optimized | 5,000 | `[INSERT_PROFILER_METRIC]` | `0 B` | 3 |
| Naive baseline | 10,000 | `[INSERT_PROFILER_METRIC]` | `[INSERT_PROFILER_METRIC]` | 10,000 |
| Optimized | 10,000 | `[INSERT_PROFILER_METRIC]` | `0 B` | 3 |

**Profiler screenshots**

![Naive baseline profiler capture](Screenshots/naive-baseline.png)

![Optimized architecture profiler capture](Screenshots/optimized.png)

> Drop your captures into `Screenshots/` and the images will render here. Engineers and recruiters trust profiler evidence far more than claims.

### Real-world pitfalls avoided

- **`ToList()` inside update loops.** The source project copied a cell occupancy list every frame (`gridcontrol.cs:348`) — a silent per-frame allocation. The centralized registry removes the need for the copy entirely.
- **`GetComponent` in hot paths.** The original `DamagePopupManager` called `GetComponent` on every popup acquisition. `ComponentCache` resolves once on rent.
- **Mid-iteration mutation.** Unregistering a tickable during dispatch is deferred to a preallocated pending queue; swap-back removal keeps indices valid without copying the list.
- **Per-object audio emitters.** The original factory created one `AudioSource` per machine. `EmitterThrottler` plus a shared source pool bounds concurrent emitters to a fixed cap.

## Installation

**Window > Package Manager > + > Add package from git URL:**

```text
https://github.com/kenjeii/Unity-HeavyObject-PerformanceCore.git
```

Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kenjeii.unity-perf-core": "https://github.com/kenjeii/Unity-HeavyObject-PerformanceCore.git"
  }
}
```

## Running the benchmark

1. Import the package and open **Package Manager > Unity Heavy Object Performance Core > Samples > Stress Test Benchmark**, or create an empty GameObject and attach `StressTestBenchmark`.
2. Choose a mode:
   - `Naive` — `N` objects, each with its own `Update()` plus periodic spawn/destroy churn.
   - `Optimized` — `N` objects driven by `CentralizedUpdateManager`, pooled with `TypedObjectPool`, sharing a `FlyweightConfig`, with time-sliced heavy work and throttled emitters.
3. Set object count (default 5,000, up to 20,000).
4. Open **Window > Analysis > Profiler**, enable **Deep Profile** if desired, and press Play.
5. Compare `GC Alloc / Frame`, `BehaviourUpdate` time and the custom `PerfCore.*` / `Benchmark.*` markers.

Call `Rebuild()` on the component to switch modes without reloading the scene.

## Repository layout

```text
Runtime/
├── Kenjeii.UnityPerfCore.Runtime.asmdef
├── CentralizedUpdate/   ITickable, ILateTickable, IFixedTickable,
│                        TickRegistry, CentralizedUpdateManager, SpriteFrameAnimator
├── ObjectPooling/       IPoolable, MonoPoolable, TypedObjectPool,
│                        PoolRegistry, ComponentCache
├── FlyweightData/       FlyweightConfig, FlyweightState, FlyweightRegistry
└── TimeSlicing/         ITimeSlicedTask, FrameBudgetScheduler,
                         EmitterThrottler, DirtyFlag
Samples~/StressTestBenchmark/
    ├── Kenjeii.UnityPerfCore.Benchmark.asmdef
    └── StressTestBenchmark.cs
```

## License

MIT. Copyright (c) 2026 Lei Yat Ming / kenjeii. See `LICENSE`.
