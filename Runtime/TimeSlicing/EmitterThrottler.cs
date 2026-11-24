using UnityEngine;

namespace Kenjeii.UnityPerfCore.TimeSlicing
{
    /// <summary>
    /// Bounds the number of simultaneous emitters (sounds, particles, VFX) spawned
    /// by a large population of objects. Uses a fixed-size ring of end timestamps
    /// and prunes expired entries in place — no lists grow, no per-emit allocation.
    /// </summary>
    public sealed class EmitterThrottler
    {
        private readonly float[] _endTimes;
        private int _cursor;

        /// <summary>Maximum simultaneous emitters allowed.</summary>
        public int MaxEmitters => _endTimes.Length;

        /// <summary>Creates a throttler with a fixed emitter cap.</summary>
        public EmitterThrottler(int maxEmitters)
        {
            _endTimes = new float[maxEmitters < 1 ? 1 : maxEmitters];
            _cursor = 0;
        }

        /// <summary>
        /// Attempts to register one emitter. Returns false when the cap is reached
        /// and no slot has expired. Does not use Time.deltaTime or coroutines.
        /// </summary>
        public bool TryEmit(float durationSeconds)
        {
            float now = Time.realtimeSinceStartup;
            int length = _endTimes.Length;

            // Ring scan: find the first expired slot starting at cursor.
            for (int i = 0; i < length; i++)
            {
                int slot = (_cursor + i) % length;
                if (_endTimes[slot] > now) continue;

                _endTimes[slot] = now + Mathf.Max(0f, durationSeconds);
                _cursor = (slot + 1) % length;
                return true;
            }
            return false;
        }

        /// <summary>Clears all active emitter timestamps.</summary>
        public void Reset()
        {
            for (int i = 0; i < _endTimes.Length; i++) _endTimes[i] = 0f;
            _cursor = 0;
        }
    }
}
