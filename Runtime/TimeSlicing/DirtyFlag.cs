namespace Kenjeii.UnityPerfCore.TimeSlicing
{
    /// <summary>
    /// Bitmask of dirty reasons. A heavy object sets bits when state changes;
    /// a single deferred pass evaluates only objects whose mask is non-zero,
    /// then clears the mask. Avoids re-evaluating the entire population each frame.
    /// </summary>
    public struct DirtyFlag
    {
        private int _bits;

        /// <summary>True when any bit is set.</summary>
        public bool IsDirty => _bits != 0;

        /// <summary>Marks one or more reasons dirty.</summary>
        public void Mark(int mask) => _bits |= mask;

        /// <summary>Clears all bits. Returns the mask that was set.</summary>
        public int Clear()
        {
            int previous = _bits;
            _bits = 0;
            return previous;
        }

        /// <summary>Tests one reason without clearing.</summary>
        public bool Has(int mask) => (_bits & mask) != 0;
    }
}
