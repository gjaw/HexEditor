namespace Gjaw.Bintools.BinFile
{
    /// <summary>
    /// The type of patch operation
    /// </summary>
    public enum PatchType
    {
        /// <summary>
        /// Unspecified value. Patches with this type are invalid.
        /// </summary>
        None,
        /// <summary>
        /// Pure insertion of data at a specified offset.
        /// </summary>
        Insert,
        /// <summary>
        /// Deletion of data at a specified offset.
        /// </summary>
        Delete,
        /// <summary>
        /// Replacing of data at a specified offset.
        /// </summary>
        Replace,
    }
}
