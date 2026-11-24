namespace Kenjeii.UnityPerfCore.FlyweightData
{
    /// <summary>
    /// Base class for immutable shared configuration. One instance is shared by all
    /// runtime objects of a kind; it must contain no per-instance state.
    /// Derive and add readonly fields only — never mutate after construction.
    /// </summary>
    public abstract class FlyweightConfig
    {
        /// <summary>Stable identifier for this configuration.</summary>
        public readonly string Id;

        /// <summary>Creates a config with an identifier.</summary>
        protected FlyweightConfig(string id) => Id = id;
    }

    /// <summary>
    /// Lightweight runtime state paired with a flyweight config. Stores only mutable
    /// per-instance values — no copies of the shared data.
    /// </summary>
    /// <typeparam name="TConfig">Shared immutable config type.</typeparam>
    public struct FlyweightState<TConfig> where TConfig : FlyweightConfig
    {
        /// <summary>Shared configuration reference.</summary>
        public TConfig Config;

        /// <summary>Per-instance alive flag.</summary>
        public bool Active;

        /// <summary>Per-instance elapsed time.</summary>
        public float Age;

        /// <summary>Binds a config and resets runtime state.</summary>
        public void Initialize(TConfig config)
        {
            Config = config;
            Active = true;
            Age = 0f;
        }
    }

    /// <summary>
    /// Registry of shared configs keyed by id. Configs are created once and reused;
    /// runtime objects hold only a FlyweightState with a reference to one.
    /// </summary>
    /// <typeparam name="TConfig">Shared immutable config type.</typeparam>
    public sealed class FlyweightRegistry<TConfig> where TConfig : FlyweightConfig
    {
        private readonly System.Collections.Generic.Dictionary<string, TConfig> _configs =
            new System.Collections.Generic.Dictionary<string, TConfig>();

        /// <summary>Number of registered configs.</summary>
        public int Count => _configs.Count;

        /// <summary>Registers or replaces a config.</summary>
        public void Register(TConfig config)
        {
            if (config != null) _configs[config.Id] = config;
        }

        /// <summary>Gets a config by id. Resolve once at spawn, not per frame.</summary>
        public bool TryGet(string id, out TConfig config) => _configs.TryGetValue(id, out config);

        /// <summary>Clears all registered configs.</summary>
        public void Clear() => _configs.Clear();
    }
}
