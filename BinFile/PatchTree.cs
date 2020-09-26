using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Gjaw.Bintools.BinFile
{
    /// <summary>
    /// PatchTree maintains the chain of changes, providing methods for resolving the current or past state.
    /// </summary>
    public class PatchTree : INotifyPropertyChanged
    {
        /// <summary>
        /// Get/Set the maximum depth of recorded steps to allow step-by-step undo.
        /// 
        /// If this is set to lower value than the current <see cref="UndoLevel"/>, then earliest undo steps are consolidated into a single undo step so that all the undo steps fit to the new capacity.
        /// </summary>
        public int UndoCapacity
        {
            get => _patches.Capacity;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Capacity must be positive");
                }
                int clevel = UndoLevel;
                if (value < clevel)
                {
                    CombineEarliestPatches(clevel - value);
                }
                _patches.Capacity = value;
                PropertyChangedInvoke(nameof(UndoCapacity));
            }
        }

        /// <summary>
        /// Get/Set the current level of undo steps.
        /// 
        /// The value can only be set to current level or lower as no "no-op" undo steps cannot be synthetized.
        /// When setting the value to lower than current level, the earliest undo steps are consolidated into a single undo step to reduce the number of undo steps to the requested level.
        /// 
        /// Setting this value will *not* perform undo/redo.
        /// </summary>
        public int UndoLevel
        {
            get => _patches.Count;
            set
            {
                int clevel = _patches.Count;
                if (value > clevel)
                {
                    throw new ArgumentException("Cannot set UndoLevel higher than current level");
                }
                if (value < clevel)
                {
                    if (value < 1)
                    {
                        throw new ArgumentException("Cannot erase the base undo step");
                    }
                    CombineEarliestPatches(clevel - value);
                }
            }
        }

        /// <summary>
        /// Get/Set the flag that controls whether undo steps should be as granular as possible (true)
        /// or if compatible subsequent edits should automatically be consolidated into a single step (false, default).
        /// </summary>
        public bool GranularUndo
        {
            get => _granular_undo;
            set
            {
                _granular_undo = value;
                PropertyChangedInvoke(nameof(GranularUndo));
            }
        }
        private bool _granular_undo;

        /// <summary>
        /// Storage of binary patches/undo steps.
        /// </summary>
        private readonly List<Patch> _patches;

        /// <summary>
        /// Source of original data
        /// </summary>
        private readonly IBinarySource _source;

        /// <summary>
        /// Event for whenever a public propery changes value.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Create a new <see cref="PatchTree"/> with default undo capacity of 500 and non-granular undo steps.
        /// </summary>
        /// <param name="source">Source of data</param>
        public PatchTree(IBinarySource source) : this(source, 500, false) { }

        /// <summary>
        /// Create a new <see cref="PatchTree"/> with specified capacity and granularity.
        /// </summary>
        /// <param name="source">Source of data</param>
        /// <param name="capacity">Number of undo steps to allow storing</param>
        /// <param name="granular_steps">Initial value for <see cref="GranularUndo"/></param>
        public PatchTree(IBinarySource source, int capacity, bool granular_steps)
        {
            _patches = new List<Patch>(capacity);
            _granular_undo = granular_steps;
            _source = source;
        }

        /// <summary>
        /// Add a new patch.
        /// </summary>
        /// <param name="patch">Patch to add</param>
        public void Add(Patch patch)
        {
            int level = UndoLevel;
            if (level > 0)
            {
                // Can we do opportunistic merge?
                if (!_granular_undo)
                {
                    Patch latest = _patches[level - 1];
                    if (latest.MergeWith(patch))
                    {
                        // merged, all good
                        return;
                    }
                }
                // Is there space, or is combining needed?
                if (level == UndoCapacity)
                {
                    CombineEarliestPatches(1);
                }
            }
            // Add the new patch
            _patches.Add(patch);
            PropertyChangedInvoke(nameof(UndoLevel));
        }

        /// <summary>
        /// Combine the requested number of patches at the start of the history.
        /// </summary>
        /// <param name="count">Number of patches to combine</param>
        private void CombineEarliestPatches(int count)
        {
            int current_level = UndoLevel;

            PropertyChangedInvoke(nameof(UndoLevel));
        }

        /// <summary>
        /// Fire event handlers for <see cref="PropertyChanged"/>.
        /// </summary>
        /// <param name="name">Name of the property that changed</param>
        protected void PropertyChangedInvoke(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
