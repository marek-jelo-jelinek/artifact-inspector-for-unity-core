namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>Byte-order-reversal helpers shared by the SerializedFile header sniff and metadata cursor reader.</summary>
    internal static class EndianUtility
    {
        internal static ushort SwapUInt16(ushort value)
        {
            return (ushort)((value << 8) | (value >> 8));
        }

        internal static uint SwapUInt32(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }

        internal static ulong SwapUInt64(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) |
                   ((value & 0x000000000000FF00UL) << 40) |
                   ((value & 0x0000000000FF0000UL) << 24) |
                   ((value & 0x00000000FF000000UL) << 8) |
                   ((value & 0x000000FF00000000UL) >> 8) |
                   ((value & 0x0000FF0000000000UL) >> 24) |
                   ((value & 0x00FF000000000000UL) >> 40) |
                   ((value & 0xFF00000000000000UL) >> 56);
        }
    }
}
