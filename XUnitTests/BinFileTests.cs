using Gjaw.Bintools.BinFile;
using System;
using System.Text;
using Xunit;

namespace Gjaw.Bintools.Tests
{
    /// <summary>
    /// Tests related to BinFile project
    /// </summary>
    public class BinFileTests
    {
        /// <summary>
        /// Simple test on insertion mode patch
        /// </summary>
        [Theory]
        [InlineData(0UL, 6)]
        [InlineData(5UL, 0)]
        [InlineData(34720895UL, 235)]
        [InlineData(3252UL, 1)]
        [InlineData(42UL, 5)]
        public void InsertionPatchTest(ulong offset, int datalen)
        {
            var data = new byte[datalen];
            var patch = Patch.NewInsertion(offset, data);
            Assert.Equal(offset, patch.StartOffset);
            Assert.Equal(offset, patch.EndOffset);
            Assert.Equal(datalen, patch.EndMove);
            Assert.Equal(PatchType.Insert, patch.Type);
        }

        /// <summary>
        /// Simple test on deletion mode patch
        /// </summary>
        [Theory]
        [InlineData(0UL, 6UL)]
        [InlineData(5UL, 0UL)]
        [InlineData(34720895UL, 235UL)]
        [InlineData(3252UL, 1UL)]
        [InlineData(42UL, 5UL)]
        public void DeletionPatchTest(ulong offset, ulong datalen)
        {
            var patch = Patch.NewDeletion(offset, datalen);
            Assert.Equal(offset, patch.StartOffset);
            Assert.Equal(offset + datalen, patch.EndOffset);
            Assert.Equal(-(long)datalen, patch.EndMove);
            Assert.Equal(PatchType.Delete, patch.Type);
        }

        /// <summary>
        /// Simple test on replacement mode patch
        /// </summary>
        [Theory]
        [InlineData(0UL, 6, 9UL)]
        [InlineData(5UL, 0, 3UL)]
        [InlineData(34720895UL, 235, 10UL)]
        [InlineData(3252UL, 1, 1UL)]
        [InlineData(42UL, 5, 0UL)]
        public void ReplacementPatchTest(ulong offset, int datalen, ulong dellen)
        {
            var data = new byte[datalen];
            var patch = Patch.NewReplacement(offset, dellen, data);
            Assert.Equal(offset, patch.StartOffset);
            Assert.Equal(offset + dellen, patch.EndOffset);
            Assert.Equal(datalen - (long)dellen, patch.EndMove);
            Assert.Equal(PatchType.Replace, patch.Type);
        }

        /// <summary>
        /// Test if the simple merge correctly identifies mergeable cases and performs the merge right.
        /// </summary>
        [Theory]
        // Insert 1 followed by insert 2
        [InlineData(1, "ab", 0, 3, "cd", 0, true, 1, 1, "abcd", 4)]
        // Insert 2 followed by insert 1
        [InlineData(1, "ab", 0, 1, "cd", 0, true, 1, 1, "cdab", 4)]
        // Insert 1 with insert 2 inside it
        [InlineData(1, "ab", 0, 2, "cd", 0, true, 1, 1, "acdb", 4)]
        // Insert 1, gap, insert 2
        [InlineData(1, "ab", 0, 4, "cd", 0, false, 1, 1, "ab", 2)]
        // Insert 2, gap, insert 1
        [InlineData(1, "ab", 0, 0, "cd", 0, false, 1, 1, "ab", 2)]
        // Two deletes
        [InlineData(1, "", 2, 1, "", 3, true, 1, 6, "", -5)]
        // Two deletes with gap after first
        [InlineData(1, "", 2, 2, "", 3, false, 1, 3, "", -2)]
        // Two deletes with short gap before first
        [InlineData(1, "", 2, 0, "", 3, true, 0, 5, "", -5)]
        // Two deletes with long gap before first
        [InlineData(5, "", 2, 0, "", 3, false, 5, 7, "", -2)]
        // Insert followed by delete
        [InlineData(1, "ab", 0, 3, "", 3, true, 1, 4, "ab", -1)]
        // Insert preceded by short delete
        [InlineData(1, "ab", 0, 1, "", 1, true, 1, 1, "b", 1)]
        // Insert preceded by long delete
        [InlineData(1, "ab", 0, 1, "", 3, true, 1, 2, "", -1)]
        // Insert with delete inside
        [InlineData(1, "abc", 0, 2, "", 1, true, 1, 1, "ac", 2)]
        // Insert with delete inside spanning over the insert
        [InlineData(1, "abc", 0, 2, "", 3, true, 1, 2, "a", 0)]
        // Replace 1 followed by replace 2
        [InlineData(1, "abc", 2, 4, "de", 1, true, 1, 4, "abcde", 2)]
        // Replace 2 eating a bit of replace 1 from start
        [InlineData(1, "abc", 2, 1, "de", 1, true, 1, 3, "debc", 2)]
        // Replace 2 eating all of replace 1 and over
        [InlineData(1, "abc", 2, 1, "de", 4, true, 1, 4, "de", -1)]
        // Replace 2 inside replace 1
        [InlineData(1, "abc", 2, 3, "de", 2, true, 1, 4, "abde", 1)]
        // Replace 2 catching up to replace 1
        [InlineData(1, "abc", 2, 0, "de", 1, true, 0, 3, "deabc", 2)]
        // Replace 2 catching up to and eating a piece of replace 1
        [InlineData(1, "abc", 2, 0, "de", 2, true, 0, 3, "debc", 1)]
        public void MergeTest(ulong offset1, string data1, ulong dellen1, ulong offset2, string data2, ulong dellen2,
            bool expected, ulong expected_start, ulong expected_end, string expected_data, long expected_move)
        {
            var patch1 = Patch.NewReplacement(offset1, dellen1, Encoding.ASCII.GetBytes(data1));
            var patch2 = Patch.NewReplacement(offset2, dellen2, Encoding.ASCII.GetBytes(data2));
            bool merge_result = patch1.MergeWith(patch2);
            Assert.Equal(expected, merge_result);
            Assert.Equal(expected_start, patch1.StartOffset);
            Assert.Equal(expected_end, patch1.EndOffset);
            Assert.Equal(expected_move, patch1.EndMove);
            Assert.Equal(Encoding.ASCII.GetBytes(expected_data), patch1.PatchData);
        }
    }
}
