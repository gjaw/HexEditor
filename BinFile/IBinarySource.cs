using System;

namespace Gjaw.Bintools.BinFile
{
    /// <summary>
    /// Interface suitable for using as a binary data source.
    /// </summary>
    public interface IBinarySource
    {
        /// <summary>
        /// Read a range of bytes into a buffer.
        /// </summary>
        /// <param name="start">Start offset into the data (inclusive)</param>
        /// <param name="count">Number of bytes to read</param>
        /// <param name="target">Target buffer to store the data to</param>
        /// <param name="target_start">Offset into the target buffer</param>
        void Read(ulong start, ulong count, byte[] target, ulong target_start);
    }
}
