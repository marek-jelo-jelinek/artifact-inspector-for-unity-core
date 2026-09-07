# Proposal: Support loose Player Build files in `ArtifactInspector`

## Problem

`ArtifactInspector.OpenAssetBundle` is the library's only entry point, and it always calls
`UFS_MountArchive`. That's correct for `.bundle`-style archives (Addressables output, asset
bundles), which are containers `UFS_MountArchive` knows how to open and enumerate entries from.

It's the wrong call for a Player Build's loose output files -- `globalgamemanagers`,
`sharedassets0.assets`, `level0`, `resources.assets`, and friends. Those are already bare
SerializedFiles sitting directly on disk; they were never wrapped in an archive container. Every
one of them fails `UFS_MountArchive` with `FileFormatError` (confirmed against a real Unity
6000.3.13f1 Player Build directory dropped into `TestData/`; every single loose file failed the
same way, including the two that are unambiguously valid SerializedFiles).

## What Unity's own API already supports

`UnityFileSystemApi.cs` already resolves and wraps two native exports that don't require a
mounted archive at all:

- `UFS_OpenFile(virtualPath)` -- opens a raw file for byte-range reads.
- `UFS_OpenSerializedFile(virtualPath)` -- opens a SerializedFile for TypeTree-based reads.

Both take a "virtual path" string. For an archive entry, `ArtifactArchive` builds that path by
prefixing the entry name with the mount point (`ResolveVirtualPath`, `ArtifactArchive.cs:124`).
But there's nothing archive-specific about the *shape* of that path as far as these two exports
are concerned -- Unity's actual UnityFileSystemApi accepts a plain OS filesystem path here too, for
exactly this loose-file case. `ArtifactArchive.OpenSerializedFile` (`ArtifactArchive.cs:168`) and
`GetOrOpenFileHandle` (`ArtifactArchive.cs:311`) already show the whole call shape needed --
`OpenSerializedFile` + `OpenFile`, wrapped in `SerializedFileHandle`/`FileHandle`, combined into a
`SerializedFile`. A loose-file path only needs to skip the mount step and pass the real file path
through instead of a mount-point-qualified virtual path.

## Proposed API

Add one new entry point next to `OpenAssetBundle`:

```csharp
public static class ArtifactInspector
{
    // Existing:
    public static ArtifactArchive OpenAssetBundle(string filePath);

    // New:
    public static SerializedFile OpenSerializedFile(string filePath);
}
```

`OpenSerializedFile` calls `api.OpenSerializedFile(filePath)` and `api.OpenFile(filePath)`
directly (no `MountArchive`, no mount point, no `ArtifactArchive` wrapper) and constructs a
`SerializedFile` exactly the way `ArtifactArchive.OpenSerializedFile` does today, minus the
archive layer. The same `SerializedFileOpenException`/`missingTypeTrees` triage
(`ArtifactArchive.cs:180-187`, `IsPositivelyMissingTypeTrees`) applies unchanged -- a stripped
Player Build file needs the same fallback path a stripped archive entry does.

Two knock-on effects to design for:

- `SerializedFile`'s constructor currently expects a `TypeTreeCache` and handles owned by the
  archive; a loose-file `SerializedFile` needs to own its own `FileHandle`/`SerializedFileHandle`
  lifetime directly (no `ArtifactArchive` to dispose them), so `Dispose()` needs to reach both.
- `ExternalReferences` on a loose file can point at *other* loose files in the same Player Build
  directory (e.g. `sharedassets0.assets` referencing `globalgamemanagers.assets`) rather than at
  archive-relative entries. That resolution is out of scope for this proposal -- flagging it so it
  isn't a surprise later, not proposing a fix now.

## What doesn't change

- `ArtifactArchive`/`OpenAssetBundle` stay exactly as they are; `.bundle` files keep working the
  way they do today.
- `SerializedFileDetector` (the from-scratch, no-native-call parser) already works on any byte
  source regardless of whether it came from an archive entry or a loose file, so it needs no
  changes.

## Test-side change

`RealTestDataIntegrationTests` currently assumes every file in `TestData/` is `OpenAssetBundle`-able.
Once `OpenSerializedFile` exists, that fixture needs to try both -- e.g. attempt `OpenAssetBundle`
first, and for files where that fails, fall back to `OpenSerializedFile` -- or split `TestData/`
into an archives lane and a loose-serialized-files lane so each is exercised through the API meant
for it.

## A blocking prerequisite, found while investigating this

While probing the real Player Build files for this proposal, actually reading byte *content*
(not just opening/mounting/listing) segfaulted the process (SIGSEGV) on the very first object of
the very first `.bundle` file too -- via `TypeTreeReader.Field(...).AsString()`, which goes through
`NativeFileByteSource.Read` (`SeekFile` + `ReadFile`). This reproduces independent of the
SerializeReference issue and independent of Player Build data entirely; it's in the shared native
byte-read path every real object read goes through. Mounting, listing entries, and reading
TypeTree shape all work fine against this same dylib -- only actual content reads crash.

This is worth calling out here because it means validating a new `OpenSerializedFile` API against
real data will hit the same wall as soon as it reads any field content, independent of whatever
this proposal adds. It's a separate, pre-existing issue (not part of this proposal) and needs its
own investigation before either path can be trusted against real data.
