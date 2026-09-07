namespace Tesearis.ArtifactInspectorForUnity.Core.Model
{
    /// <summary>The kind of external reference, as returned by UFS_GetExternalReference.</summary>
    public enum ExternalReferenceType
    {
        NonAssetType = 0,
        DeprecatedCachedAssetType = 1,
        SerializedAssetType = 2,
        MetaAssetType = 3,
    }
}
