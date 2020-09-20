using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gjaw.Bintools.BinFile
{
    /// <summary>
    /// PatchTree maintains the chain of changes, providing methods for resolving the current or past state.
    /// </summary>
    public class PatchTree
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
        public bool GranularUndo { get; set; }

        /// <summary>
        /// Storage of binary patches/undo steps.
        /// </summary>
        private readonly List<Patch> _patches;

        /// <summary>
        /// Create a new <see cref="PatchTree"/> with default undo capacity of 500 and non-granular undo steps.
        /// </summary>
        public PatchTree() : this(500, false) { }

        /// <summary>
        /// Create a new <see cref="PatchTree"/> with specified capacity and granularity.
        /// </summary>
        /// <param name="capacity">Number of undo steps to allow storing</param>
        /// <param name="granular_steps">Initial value for <see cref="GranularUndo"/></param>
        public PatchTree(int capacity, bool granular_steps)
        {
            _patches = new List<Patch>(capacity);
            GranularUndo = granular_steps;
        }

        /// <summary>
        /// Combine the requested number of patches at the start of the history.
        /// </summary>
        /// <param name="count">Number of patches to combine</param>
        private void CombineEarliestPatches(int count)
        {
            throw new NotImplementedException();
        }
    }
}
