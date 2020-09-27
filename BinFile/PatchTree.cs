using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection.Metadata;

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
                    if (latest.TryMergeWith(patch))
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
        /// Remove most recent patches.
        /// </summary>
        /// <param name="count">Number of patches to remove (default: 1)</param>
        public void Undo(int count = 1)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive");
            }
            if (count > UndoLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot undo more times than there are steps to undo");
            }
            _patches.RemoveRange(_patches.Count - count, count);
            PropertyChangedInvoke(nameof(UndoLevel));
        }

        /// <summary>
        /// Combine the requested number of patches at the start of the history.
        /// </summary>
        /// <param name="count">Number of patches to combine</param>
        private void CombineEarliestPatches(int count)
        {
            int current_level = UndoLevel;
            Patch first = _patches[0];
            int source;
            for (source = 1; source <= count; source++)
            {
                first = CombinePatches(first, _patches[source]);
            }
            _patches[0] = first;
            int target = 1;
            for (source++; source < current_level; source++)
            {
                _patches[target] = _patches[source];
                target++;
            }
            _patches.RemoveRange(target, count);
            PropertyChangedInvoke(nameof(UndoLevel));
        }

        /// <summary>
        /// Combine two patches. Will try merge, falling back to creating a new bigger all-compassing patch.
        /// </summary>
        /// <param name="p1">First patch</param>
        /// <param name="p2">Second patch</param>
        /// <returns>Combined patch</returns>
        private Patch CombinePatches(Patch p1, Patch p2)
        {
            // Try merge
            if (p1.TryMergeWith(p2))
            {
                return p1;
            }
            // Find the range needed by the two patches
            ulong start = p1.StartOffset;
            ulong end = p1.EndOffset;
            if (p2.StartOffset > p1.EndOffset)
            {
                // p2 is after p1
                end = (ulong)((long)p2.EndOffset - p1.EndMove);
            }
            else if (p2.StartOffset < p1.StartOffset)
            {
                // p2 precedes p1
                start = p2.StartOffset;
                end = p1.EndOffset;
            }
            else
            {
                throw new InvalidOperationException($"BUG! Merge ought to have worked. Please include this data in bug report: CombinePatches({p1}, {p2})");
            }
            // Create patch
            ulong count = end - start;
            int tbuflen = (int)count;
            long buflen = tbuflen;
            if (p1.EndMove > 0) buflen += p1.EndMove;
            if (p2.EndMove > 0) buflen += p2.EndMove;
            byte[] buffer = new byte[buflen];
            _source.Read(start, end-start, buffer, 0);
            tbuflen += ApplyPatch(buffer, p1, start, tbuflen);
            tbuflen += ApplyPatch(buffer, p2, start, tbuflen);
            return new Patch(start, end - start, buffer[0..tbuflen]);
        }

        /// <summary>
        /// Apply a patch to data buffer.
        /// </summary>
        /// <param name="buffer">Buffer to modify</param>
        /// <param name="patch">Patch to apply to the buffer</param>
        /// <param name="offset">Start offset of the buffer to logical start</param>
        /// <param name="buflen">Current buffer active length</param>
        /// <returns>Buffer active length change</returns>
        private int ApplyPatch(byte[] buffer, Patch patch, ulong offset, int buflen)
        {
            int start = (int)(patch.StartOffset - offset);
            int endmove = (int)patch.EndMove;
            int endstart = (int)(patch.EndOffset - offset + 1);
            // First move existing data to new position, if needed
            if (endmove != 0)
            {
                int count = buflen - endstart;
                Span<byte> target = new Span<byte>(buffer, endstart + endmove, count);
                Span<byte> source = new Span<byte>(buffer, endstart, count);
                if (!source.TryCopyTo(target))
                {
                    throw new InvalidOperationException($"BUG! ApplyPatch failed to move original data from 1 span to another. Bug data: ApplyPatch([{buffer.Length}], {patch}, {offset})");
                }
            }
            // Then copy patch data to its slot, if needed
            Span<byte> dsource = patch.PatchData;
            if (!dsource.IsEmpty)
            {
                Span<byte> dtarget = new Span<byte>(buffer, start, dsource.Length);
                if (!dsource.TryCopyTo(dtarget))
                {
                    throw new InvalidOperationException($"BUG! ApplyPatch failed to copy patch data from 1 span to another. Bug data: ApplyPatch([{buffer.Length}], {patch}, {offset})");
                }
            }
            // Return offset difference
            return endmove;
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
