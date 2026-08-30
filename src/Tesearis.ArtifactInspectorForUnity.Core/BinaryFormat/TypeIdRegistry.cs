using System.Collections.Generic;

namespace Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat
{
    /// <summary>
    /// Static lookup from a Unity ClassID (the numeric type identifier stored in every serialized
    /// object) to its class name. This is the only way to label an object's type in a stripped
    /// file: with no TypeTree, there's no schema to read a type name from, only the numeric ClassID
    /// <see cref="StrippedObjectInfo"/> carries.
    ///
    /// The table below (including the large hashed IDs for newer built-in types, which aren't
    /// listed on Unity's public https://docs.unity3d.com/Manual/ClassIDReference.html manual page)
    /// is sourced from Unity's own <c>TypeIdRegistry</c> in
    /// <see href="https://github.com/Unity-Technologies/UnityDataTools">UnityDataTools</see>, which
    /// generates it from Unity's internal engine type list. UnityDataTools is licensed under the
    /// Unity Companion License (https://unity3d.com/legal/licenses/unity_companion_license); this
    /// data is used here under that license rather than under this project's own MIT license -- see
    /// ThirdPartyNotices.md.
    /// </summary>
    public static class TypeIdRegistry
    {
        private static readonly Dictionary<int, string> KnownTypes = new()
        {
            { 1, "GameObject" },
            { 2, "Component" },
            { 4, "Transform" },
            { 8, "Behaviour" },
            { 18, "EditorExtension" },
            { 25, "Renderer" },
            { 56, "Collider" },
            { 130, "NamedObject" },
            { 3, "LevelGameManager" },
            { 5, "TimeManager" },
            { 6, "GlobalGameManager" },
            { 9, "GameManager" },
            { 11, "AudioManager" },
            { 13, "InputManager" },
            { 19, "Physics2DSettings" },
            { 30, "GraphicsSettings" },
            { 47, "QualitySettings" },
            { 55, "PhysicsManager" },
            { 78, "TagManager" },
            { 104, "RenderSettings" },
            { 126, "NavMeshProjectSettings" },
            { 129, "PlayerSettings" },
            { 141, "BuildSettings" },
            { 236, "ClusterInputManager" },
            { 300, "RuntimeInitializeOnLoadManager" },
            { 310, "UnityConnectSettings" },
            { 20, "Camera" },
            { 21, "Material" },
            { 23, "MeshRenderer" },
            { 27, "Texture" },
            { 28, "Texture2D" },
            { 29, "OcclusionCullingSettings" },
            { 33, "MeshFilter" },
            { 41, "OcclusionPortal" },
            { 43, "Mesh" },
            { 45, "Skybox" },
            { 48, "Shader" },
            { 72, "ComputeShader" },
            { 84, "RenderTexture" },
            { 86, "CustomRenderTexture" },
            { 89, "Cubemap" },
            { 96, "TrailRenderer" },
            { 108, "Light" },
            { 111, "Animation" },
            { 117, "Texture3D" },
            { 119, "Projector" },
            { 120, "LineRenderer" },
            { 121, "Flare" },
            { 122, "Halo" },
            { 123, "LensFlare" },
            { 124, "FlareLayer" },
            { 137, "SkinnedMeshRenderer" },
            { 152, "MovieTexture" },
            { 156, "TerrainData" },
            { 157, "LightmapSettings" },
            { 158, "WebCamTexture" },
            { 171, "SparseTexture" },
            { 187, "Texture2DArray" },
            { 188, "CubemapArray" },
            { 191, "OffMeshLink" },
            { 192, "OcclusionArea" },
            { 193, "Tree" },
            { 198, "ParticleSystem" },
            { 199, "ParticleSystemRenderer" },
            { 200, "ShaderVariantCollection" },
            { 205, "LODGroup" },
            { 210, "SortingGroup" },
            { 212, "SpriteRenderer" },
            { 213, "Sprite" },
            { 215, "ReflectionProbe" },
            { 218, "Terrain" },
            { 220, "LightProbeGroup" },
            { 222, "CanvasRenderer" },
            { 226, "BillboardAsset" },
            { 227, "BillboardRenderer" },
            { 258, "LightProbes" },
            { 259, "LightProbeProxyVolume" },
            { 331, "SpriteMask" },
            { 363, "OcclusionCullingData" },
            { 687078895, "SpriteAtlas" },
            { 850595691, "LightingSettings" },
            { 1953259897, "TerrainLayer" },
            { 1839735485, "Tilemap" },
            { 483693784, "TilemapRenderer" },
            { 50, "Rigidbody2D" },
            { 53, "Collider2D" },
            { 54, "Rigidbody" },
            { 57, "Joint" },
            { 58, "CircleCollider2D" },
            { 59, "HingeJoint" },
            { 60, "PolygonCollider2D" },
            { 61, "BoxCollider2D" },
            { 62, "PhysicsMaterial2D" },
            { 64, "MeshCollider" },
            { 65, "BoxCollider" },
            { 66, "CompositeCollider2D" },
            { 68, "EdgeCollider2D" },
            { 70, "CapsuleCollider2D" },
            { 75, "ConstantForce" },
            { 134, "PhysicsMaterial" },
            { 135, "SphereCollider" },
            { 136, "CapsuleCollider" },
            { 138, "FixedJoint" },
            { 143, "CharacterController" },
            { 144, "CharacterJoint" },
            { 145, "SpringJoint" },
            { 146, "WheelCollider" },
            { 153, "ConfigurableJoint" },
            { 154, "TerrainCollider" },
            { 171741748, "ArticulationBody" },
            { 19719996, "TilemapCollider2D" },
            { 81, "AudioListener" },
            { 82, "AudioSource" },
            { 83, "AudioClip" },
            { 164, "AudioReverbFilter" },
            { 165, "AudioHighPassFilter" },
            { 166, "AudioChorusFilter" },
            { 167, "AudioReverbZone" },
            { 168, "AudioEchoFilter" },
            { 169, "AudioLowPassFilter" },
            { 170, "AudioDistortionFilter" },
            { 180, "AudioBehaviour" },
            { 181, "AudioFilter" },
            { 240, "AudioMixer" },
            { 241, "AudioMixerController" },
            { 243, "AudioMixerGroupController" },
            { 245, "AudioMixerSnapshotController" },
            { 272, "AudioMixerSnapshot" },
            { 273, "AudioMixerGroup" },
            { 74, "AnimationClip" },
            { 90, "Avatar" },
            { 91, "AnimatorController" },
            { 93, "RuntimeAnimatorController" },
            { 95, "Animator" },
            { 221, "AnimatorOverrideController" },
            { 319, "AvatarMask" },
            { 320, "PlayableDirector" },
            { 102, "TextMesh" },
            { 128, "Font" },
            { 223, "Canvas" },
            { 224, "RectTransform" },
            { 225, "CanvasGroup" },
            { 49, "TextAsset" },
            { 115, "MonoScript" },
            { 116, "MonoManager" },
            { 142, "AssetBundle" },
            { 150, "PreloadData" },
            { 290, "AssetBundleManifest" },
            { 328, "VideoPlayer" },
            { 329, "VideoClip" },
            { 1125, "BuildReport" },
            { 1126, "PackedAssets" },
            { 114, "MonoBehaviour" },
            { 182, "WindZone" },
            { 183, "Cloth" },
            { 195, "NavMeshAgent" },
            { 196, "NavMeshSettings" },
            { 208, "NavMeshObstacle" },
            { 238, "NavMeshData" },

            // Primitive/value placeholders that can appear as a persistentTypeID in older files
            { 100000, "int" },
            { 100001, "bool" },
            { 100002, "float" },
            { 100003, "MonoObject" },
            { 100005, "Vector3f" },
            { 100011, "void" },
        };

        /// <param name="typeId">The Unity ClassID.</param>
        /// <returns>The class name, or the ClassID formatted as a string if it isn't in the table.</returns>
        public static string GetTypeName(int typeId)
        {
            return KnownTypes.TryGetValue(typeId, out var name) ? name : typeId.ToString();
        }

        /// <summary>
        /// Strict variant of <see cref="GetTypeName"/>: returns false for an unrecognized ClassID
        /// instead of substituting the numeric id, so a caller can distinguish "really is this type"
        /// from "no name on record for this id".
        /// </summary>
        public static bool TryGetTypeName(int typeId, out string typeName)
        {
            return KnownTypes.TryGetValue(typeId, out typeName);
        }
    }
}
