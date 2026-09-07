using System;
using System.Collections.Generic;
using System.Text;
using Tesearis.ArtifactInspectorForUnity.Core.TypeTree;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Best-effort <c>m_Name</c> extraction for a <see cref="StrippedObjectInfo"/>, with no TypeTree
    /// and no schema -- relies entirely on Unity's serialization order being base-class-fields-first.
    /// <c>NamedObject</c> contributes exactly one field, <c>string m_Name</c>, ahead of anything its
    /// concrete subclass (<c>Texture2D</c>, <c>Mesh</c>, <c>AudioClip</c>, ...) adds, so for those
    /// classes it's reliably the first bytes of the object. <c>MonoBehaviour</c> isn't a
    /// <c>NamedObject</c> but carries <c>m_Name</c> at a well-documented fixed offset instead. Every
    /// other class (notably <c>GameObject</c> and every <c>Component</c>/<c>Behaviour</c> subtype,
    /// none of which are <c>NamedObject</c>s) is left alone entirely -- this is deliberately not a
    /// general field decoder, only ever degrading to <c>null</c>, never throwing or guessing.
    /// </summary>
    internal static class StrippedObjectNameReader
    {
        private const int MonoBehaviourClassId = 114;

        /// <summary>
        /// <c>m_Name</c> sits at byte offset 0 for these classes: direct-or-indirect <c>NamedObject</c>
        /// subtypes, which is exactly why it's safe to read without a TypeTree. Excludes
        /// <c>GameObject</c>, every <c>Component</c>/<c>Behaviour</c> subtype, and the singleton
        /// manager/settings classes -- none of those are <c>NamedObject</c>s.
        /// </summary>
        private static readonly HashSet<int> LeadingNameClassIds = new()
        {
            28, // Texture2D
            117, // Texture3D
            187, // Texture2DArray
            89, // Cubemap
            188, // CubemapArray
            84, // RenderTexture
            86, // CustomRenderTexture
            213, // Sprite
            43, // Mesh
            83, // AudioClip
            21, // Material
            48, // Shader
            72, // ComputeShader
            74, // AnimationClip
            90, // Avatar
            91, // AnimatorController
            221, // AnimatorOverrideController
            134, // PhysicMaterial
            62, // PhysicsMaterial2D
            49, // TextAsset
            128, // Font
            329, // VideoClip
            240, // AudioMixer
            241, // AudioMixerController
            243, // AudioMixerGroupController
            245, // AudioMixerSnapshotController
            272, // AudioMixerSnapshot
            273, // AudioMixerGroup
            200, // ShaderVariantCollection
            687078895, // SpriteAtlas
            156, // TerrainData
            1953259897, // TerrainLayer
            238, // NavMeshData
            850595691, // LightingSettings
            121, // Flare
        };

        /// <summary>
        /// <c>MonoBehaviour</c>'s layout ahead of <c>m_Name</c>: <c>PPtr&lt;GameObject&gt;
        /// m_GameObject</c> (int32 file ID + int64 path ID = 12 bytes) + <c>UInt8 m_Enabled</c>
        /// (1 byte, then 3 bytes padding to the next 4-byte-aligned field) + <c>PPtr&lt;MonoScript&gt;
        /// m_Script</c> (12 bytes) = 28 bytes.
        /// </summary>
        private const int MonoBehaviourNameOffset = 28;

        /// <summary>A name longer than this doesn't look like a real Unity object name -- treated as a misread rather than accepted as-is.</summary>
        private const int MaxPlausibleNameLength = 512;

        internal static string TryReadName(IRandomAccessByteSource source, int classId, long byteOffset, long byteSize, bool bigEndian)
        {
            if (LeadingNameClassIds.Contains(classId))
            {
                return TryReadLeadingString(source, byteOffset, byteSize, bigEndian);
            }

            if (classId == MonoBehaviourClassId)
            {
                return TryReadLeadingString(source, byteOffset + MonoBehaviourNameOffset,
                    byteSize - MonoBehaviourNameOffset, bigEndian);
            }

            return null;
        }

        /// <summary>
        /// Reads one Unity length-prefixed UTF-8 string (int32 length, then that many bytes -- no
        /// null terminator) at <paramref name="offset"/>. Never throws: returns <c>null</c> instead
        /// for anything that doesn't look like a valid name -- a short read, a length of 0 or one
        /// that doesn't fit the bytes actually available, or bytes that aren't valid, printable UTF-8
        /// -- since a name reader this speculative must never surface garbage as a "real" answer.
        /// </summary>
        private static string TryReadLeadingString(IRandomAccessByteSource source, long offset, long availableBytes, bool bigEndian)
        {
            if (offset < 0 || availableBytes < 4 || offset + 4 > source.Length)
            {
                return null;
            }

            var lengthBuffer = new byte[4];
            if (source.Read(offset, lengthBuffer, 0, 4) != 4)
            {
                return null;
            }

            var length = bigEndian ? EndianUtility.SwapUInt32(BitConverter.ToUInt32(lengthBuffer, 0)) : BitConverter.ToUInt32(lengthBuffer, 0);
            if (length == 0 || length > MaxPlausibleNameLength || length > availableBytes - 4)
            {
                return null;
            }

            var contentOffset = offset + 4;
            if (contentOffset + length > source.Length)
            {
                return null;
            }

            var contentBuffer = new byte[length];
            if (source.Read(contentOffset, contentBuffer, 0, (int)length) != length)
            {
                return null;
            }

            string name;
            try
            {
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                name = encoding.GetString(contentBuffer);
            }
            catch (DecoderFallbackException)
            {
                return null;
            }

            return IsPlausibleName(name) ? name : null;
        }

        private static bool IsPlausibleName(string name)
        {
            foreach (var c in name)
            {
                if (char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
