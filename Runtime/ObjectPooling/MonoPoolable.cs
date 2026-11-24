using UnityEngine;

namespace Kenjeii.UnityPerfCore.ObjectPooling
{
    /// <summary>
    /// Implemented by a pool so pooled MonoBehaviours can return themselves without
    /// holding a reference to the concrete generic pool type.
    /// </summary>
    public interface IPoolReturner
    {
        /// <summary>Returns one item to the owning pool.</summary>
        void Return(IPoolable item);
    }

    /// <summary>
    /// MonoBehaviour base for pooled objects. Holds an <see cref="IPoolReturner"/>
    /// reference bound once at creation; Release() needs no lookup or allocation.
    /// Subclasses override the virtual hooks to reset their own state.
    /// </summary>
    public abstract class MonoPoolable : MonoBehaviour, IPoolable
    {
        private IPoolReturner _returner;
        private bool _rented;

        /// <summary>True while this object is currently rented.</summary>
        public bool IsRented => _rented;

        /// <summary>Binds the object to its owning pool. Called once at creation.</summary>
        public void SetReturner(IPoolReturner returner) => _returner = returner;

        /// <summary>Returns this object to its pool. Zero allocation.</summary>
        public void Release()
        {
            if (!_rented) return;
            _returner?.Return(this);
        }

        /// <inheritdoc />
        public virtual void OnRent()
        {
            _rented = true;
            gameObject.SetActive(true);
        }

        /// <inheritdoc />
        public virtual void OnReturn()
        {
            _rented = false;
            gameObject.SetActive(false);
        }
    }
}
