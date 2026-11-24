namespace Kenjeii.UnityPerfCore.ObjectPooling
{
    /// <summary>
    /// Implemented by objects that live inside a pool. Lifecycle hooks are called
    /// on every rent and return so the object is reset without Instantiate/Destroy.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called immediately after the object is rented.</summary>
        void OnRent();

        /// <summary>Called immediately before the object returns to the pool.</summary>
        void OnReturn();
    }

    /// <summary>Handle that lets a pooled object return itself.</summary>
    public interface IPoolHandle
    {
        /// <summary>Returns the owning object to its pool.</summary>
        void Release();

        /// <summary>True while the object is currently rented.</summary>
        bool IsRented { get; }
    }
}
