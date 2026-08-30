using System;

namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>
    /// Formats Unity's on-disk <c>GUID</c> and <c>Hash128</c> encodings into the same
    /// 32-character lowercase hex strings Unity itself displays/produces. Neither format matches
    /// standard hex formatting: Unity's <c>GUID</c> reverses nibble order within each of its four
    /// <see cref="uint"/> words, while <c>Hash128</c> is straightforward in-order hex.
    ///
    /// Use this for <c>GUID</c>/<c>Hash128</c>-typed object fields read raw off a
    /// <see cref="TypeTree.TypeTreeReader"/> (e.g. via <c>ReadRawBytes</c>). It is not needed for
    /// <see cref="SerializedFile"/>'s external references.
    /// </summary>
    public static class GuidFormatting
    {
        private const string HexChars = "0123456789abcdef";

        /// <summary>
        /// Converts a Unity GUID's 4 raw <see cref="uint"/> words into its 32-character lowercase
        /// hex string. Unity's GUID-to-string conversion reverses nibble order within each 32-bit
        /// word relative to normal big-endian hex formatting.
        /// </summary>
        public static string FormatUnityGuid(uint d0, uint d1, uint d2, uint d3)
        {
            var result = new char[32];
            FormatWordReversed(d0, result, 0);
            FormatWordReversed(d1, result, 8);
            FormatWordReversed(d2, result, 16);
            FormatWordReversed(d3, result, 24);
            return new string(result);
        }

        /// <summary>
        /// Converts a Unity <c>Hash128</c>'s 16 raw bytes into its 32-character lowercase hex
        /// string. Unlike <see cref="FormatUnityGuid"/>, the bytes are emitted in order, with no
        /// nibble reversal.
        /// </summary>
        public static string FormatUnityHash128(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != 16)
                throw new ArgumentException("Unity Hash128 is exactly 16 bytes.", nameof(bytes));

            var result = new char[32];
            for (var i = 0; i < 16; i++)
            {
                result[i * 2] = HexChars[bytes[i] >> 4];
                result[i * 2 + 1] = HexChars[bytes[i] & 0xF];
            }
            return new string(result);
        }

        private static void FormatWordReversed(uint value, char[] output, int wordStart)
        {
            for (var j = 7; j >= 0; j--)
            {
                var nibble = (value >> (j * 4)) & 0xF;
                output[wordStart + j] = HexChars[(int)nibble];
            }
        }
    }
}
