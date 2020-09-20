using System;
using System.Collections.Generic;
using System.Linq;

namespace Gjaw.Bintools.BinFile
{
    /// <summary>
    /// Binary patch, describing a change to previous state of data.
    /// </summary>
    public class Patch
    {
        /// <summary>
        /// The starting offset into the data at its state just before applying this patch.
        /// 
        /// <see cref="StartOffset"/> and <see cref="EndOffset"/> can be used to determine which part of the original data will be edited by this patch.
        /// Note that for pure insertion both start and end offsets are the same value as no original data is edited, only new data is added.
        /// </summary>
        public ulong StartOffset { get; private set; }

        /// <summary>
        /// The ending offset into the data at its state just before applying this patch.
        /// 
        /// <see cref="StartOffset"/> and <see cref="EndOffset"/> can be used to determine which part of the original data will be edited by this patch.
        /// Note that for pure insertion both start and end offsets are the same value as no original data is edited, only new data is added.
        /// </summary>
        public ulong EndOffset { get; private set; }

        /// <summary>
        /// The number of bytes the endpoint is moved as a result of applying this patch.
        /// </summary>
        public long EndMove { get; private set; }

        /// <summary>
        /// Type of patching.
        /// </summary>
        public PatchType Type { get; private set; }

        /// <summary>
        /// Storage of inserted/replaced data.
        /// </summary>
        private byte[] _patchdata;

        /// <summary>
        /// Get a copy of the patch data.
        /// </summary>
        public byte[] PatchData => _patchdata.ToArray();

        /// <summary>
        /// Create a new patch for given data.
        /// </summary>
        /// <param name="data">Data related to the patch</param>
        private Patch(IEnumerable<byte>? data)
        {
            if (data is null)
            {
                _patchdata = Array.Empty<byte>();
            }
            else
            {
                _patchdata = data.ToArray();
            }
        }

        /// <summary>
        /// Create an insertion patch.
        /// </summary>
        /// <param name="offset">Offset at which to apply the patch</param>
        /// <param name="data">Data to insert</param>
        /// <returns>Patch object representing the insertion action</returns>
        public static Patch NewInsertion(ulong offset, IEnumerable<byte> data)
        {
            var patch = new Patch(data)
            {
                StartOffset = offset,
                EndOffset = offset,
                Type = PatchType.Insert
            };
            patch.EndMove = patch._patchdata.Length;
            return patch;
        }

        /// <summary>
        /// Create a deletion patch.
        /// </summary>
        /// <param name="offset">Offset at which to apply the patch</param>
        /// <param name="span">Number of bytes to delete</param>
        /// <returns>Patch object representing the deletion action</returns>
        public static Patch NewDeletion(ulong offset, ulong span)
        {
            var patch = new Patch(null)
            {
                StartOffset = offset,
                EndOffset = offset+span,
                Type = PatchType.Delete
            };
            patch.EndMove = -(long)span;
            return patch;
        }

        /// <summary>
        /// Create a replacement patch.
        /// </summary>
        /// <param name="offset">Offset at which to apply the patch</param>
        /// <param name="span">Number of bytes of original to replace</param>
        /// <param name="data">Data to replace the old data</param>
        /// <returns>Patch object representing the deletion action</returns>
        public static Patch NewReplacement(ulong offset, ulong span, IEnumerable<byte> data)
        {
            var patch = new Patch(data)
            {
                StartOffset = offset,
                EndOffset = offset+span,
                Type = PatchType.Replace
            };
            patch.EndMove = patch._patchdata.Length - (long)span;
            return patch;
        }

        /// <summary>
        /// Merge a patch into this patch. It is assumed that the other patch would be applied after this patch.
        /// This method is only able to perform simple merges where the two patches are mutually compatible to be represented as a single patch.
        /// </summary>
        /// <param name="other">Other patch to merge into this patch</param>
        /// <returns>true if the merge is successful, false if a simple merge cannot be performed</returns>
        public bool MergeWith(Patch other)
        {
            // First check the possibility of performing a simple merge (= no gaps)
            if (other.EndOffset < StartOffset) return false; // there would be a gap before start of this patch
            ulong new_end = (ulong)((long)EndOffset + EndMove); // end position of this patch after applying it
            if (other.StartOffset > new_end) return false; // there would be a gap after the end of this patch
            // Simple merge is possible. For efficiency check special cases of non-overlapping changes:
            ulong span = EndOffset - StartOffset;
            if (other.StartOffset == new_end)
            {
                // Most common case, the merged edit is right after the current one
                if (other._patchdata.Length > 0) _patchdata = _patchdata.Concat(other._patchdata).ToArray();
                EndOffset += other.EndOffset - other.StartOffset;
                EndMove += other.EndMove;
            }
            else if (other.EndOffset == StartOffset)
            {
                // Merged edit is just before the current one
                if (other._patchdata.Length > 0) _patchdata = other._patchdata.Concat(_patchdata).ToArray();
                StartOffset = other.StartOffset;
                EndOffset = other.EndOffset + span;
                EndMove += other.EndMove;
            }
            else if (other.StartOffset <= StartOffset)
            {
                // Overlapping edit with the other starting earlier than current patch
                int eaten = (int)(other.EndOffset - StartOffset);
                ulong prespan = StartOffset - other.StartOffset;
                long postspan = (long)other.EndOffset - EndMove - (long)EndOffset;
                if (postspan < 0) postspan = 0;
                ulong total_span = prespan + span + (ulong)postspan;
                StartOffset = other.StartOffset;
                EndOffset = StartOffset + total_span;
                EndMove += other.EndMove;
                if (eaten >= _patchdata.Length)
                {
                    // the other patch totally replaces current patch
                    _patchdata = other._patchdata.ToArray();
                }
                else
                {
                    // the other patch partially replaces current patch
                    _patchdata = other._patchdata.Concat(_patchdata.Skip(eaten)).ToArray();
                }
            }
            else
            {
                // Overlapping edit with this patch starting earlier than the other patch
                int own = (int)(other.StartOffset - StartOffset);
                int eaten = (int)(other.EndOffset - other.StartOffset);
                int remaining = _patchdata.Length - own - eaten;
                ulong postspan = 0;
                if (remaining < 0)
                {
                    postspan = (ulong)-remaining;
                    remaining = 0;
                }
                byte[] newpatch = new byte[own + other._patchdata.Length + remaining];
                if (own > 0) Array.Copy(_patchdata, newpatch, own);
                Array.Copy(other._patchdata, 0, newpatch, own, other._patchdata.Length);
                if (remaining > 0) Array.Copy(_patchdata, _patchdata.Length - remaining, newpatch, own + other._patchdata.Length, remaining);
                _patchdata = newpatch;
                EndOffset = StartOffset + span + postspan;
                EndMove += other.EndMove;
            }
            if (StartOffset == EndOffset && EndMove == _patchdata.Length) Type = PatchType.Insert;
            else if (_patchdata.Length == 0 && (long)span == -EndMove) Type = PatchType.Delete;
            else Type = PatchType.Replace;
            return true;
        }
    }
}
