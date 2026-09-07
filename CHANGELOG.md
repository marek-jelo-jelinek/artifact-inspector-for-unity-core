# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- `ArtifactArchive` now probes, once per mounted archive, whether the loaded native `UnityFileSystemApi`
  actually honors the mount-point identifier passed to `UFS_MountArchive` when later resolving
  `UFS_OpenFile`/`UFS_OpenSerializedFile` virtual paths. At least one real build (confirmed on Unity
  6000.3.13f1's macOS Editor) mounts an archive and lists its nodes fine via the handle-based calls, but
  silently ignores that identifier for path *resolution*: every `"archive:"` virtual path resolves through
  one flat, un-namespaced root instead, so the previously-hardcoded mount-scoped form
  (`"archive://<guid>/CAB-xxx"`) 404s there, while the bare form (`"archive:/CAB-xxx"`) opens fine. This made
  every `SerializedFile` entry inside every mounted archive fail to open on that native library -- confirmed
  against real Addressables/AssetBundle output, where `ArtifactArchive.OpenSerializedFile` failed for every
  single entry in every single bundle. The probe is deliberately a single `UFS_OpenFile`/`UFS_CloseFile`
  round-trip against the archive's first entry (not `UFS_OpenSerializedFile`, which can legitimately fail for
  an unrelated reason -- a stripped, no-TypeTree entry -- regardless of which virtual-path form is correct),
  cached for the lifetime of the `ArtifactArchive`, rather than retried per call: repeatedly issuing a native
  open already known to fail was observed to eventually crash the native library's process outright.

### Known issues (not yet resolved)

- Even with the fix above, standalone testing against real Addressables/AssetBundle archives (outside a
  running Unity Editor, driving the same native library directly) found that reading actual field-level data
  through an archive-mounted `FileHandle` -- `ArtifactArchive.ReadRawEntry`/`OpenRawByteSource` (used by every
  adapter's `ReadRawData`), and even just walking a `TypeTreeReader`'s field structure (`HasField`/`Field`,
  needed by every typed adapter) -- can crash the host process (a native segfault) for some entries, in ways
  that don't reproduce for a plain non-archived file and aren't specific to any one class. Listing objects
  (class name, byte size, `SerializedFile.Objects`/`ObjectRef` metadata) does not hit this path and was not
  observed to crash. Needs verification from inside an actual running Unity Editor process (this reproduction
  used a standalone .NET host loading the native library directly) before this can be considered understood,
  let alone fixed.

## [1.0.0] - 2026-08-30

Initial release.

### Added

- `ArtifactInspector`: entry point for mounting built Unity artifacts, plus process-wide setup and version introspection.
- `ArtifactArchive` / `SerializedFile` / `ObjectRef`: the core model for mounting an archive, opening its
  `SerializedFile` entries, and enumerating the objects inside them, including PPtr/external-reference resolution.
- `TypeTreeReader` / `TypeTreeNode`: lazy, random-access reading of an object's fields straight off its type tree,
  with no bulk decoding or copying until a field is actually asked for.
- `IArtifactAdapter` / `ArtifactAdapter<T>` / `ArtifactAdapterRegistry`: the extensibility mechanism for turning a
  raw type-tree reader into a typed, tolerant view of a known object type.
- Targets `netstandard2.0` and `net8.0`, with no compile-time dependency on Unity.

### Known limitations

- `[SerializeReference]` (polymorphic managed reference) fields are not decoded; `TypeTreeNode` deliberately throws
  rather than silently misreading them.
- No support for Unity WebGL's separate `UnityWebData1.0` container format. 
