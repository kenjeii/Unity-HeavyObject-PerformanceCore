namespace Kenjeii.UnityPerfCore.CentralizedUpdate
{
    /// <summary>
    /// Implemented by objects whose per-frame work should be driven by a centralized
    /// update loop instead of their own MonoBehaviour.Update. Avoids one native-to-managed
    /// call per object per frame.
    /// </summary>
    public interface ITickable
    {
        /// <summary>Advance one simulation frame.</summary>
        void Tick(float deltaTime);
    }

    /// <summary>Late-phase counterpart of <see cref="ITickable"/>.</summary>
    public interface ILateTickable
    {
        /// <summary>Advance one late frame, after all simulation ticks.</summary>
        void LateTick(float deltaTime);
    }

    /// <summary>Fixed-step counterpart of <see cref="ITickable"/>.</summary>
    public interface IFixedTickable
    {
        /// <summary>Advance one fixed physics step.</summary>
        void FixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// Optional priority hint. Lower values tick first within a bucket. The registry
    /// reads this once at registration; changing it at runtime has no effect until
    /// the object is re-registered.
    /// </summary>
    public interface ITickPriority
    {
        /// <summary>Execution priority. Lower runs first.</summary>
        int TickPriority { get; }
    }
}
