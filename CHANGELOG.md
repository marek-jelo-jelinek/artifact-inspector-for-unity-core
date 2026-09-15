# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- **Breaking:** `SerializedFileDetector.IsMissingTypeTrees` / `YamlSerializedFileDetector.IsYamlSerializedFile` now throw on invalid paths instead of returning `false`.
- **Breaking:** `ExternalReferenceType` moved from `Native` to `Model` namespace.
- `TypeTreeNode` no longer throws eagerly for unsupported `[SerializeReference]` shapes; the error is now deferred until that field is read.

### Added

- `ArtifactInspector.OpenSerializedFile(filePath)`: opens loose, non-archive SerializedFiles directly on disk (Player Build output like `sharedassets0.assets`, `globalgamemanagers`).
- Added `orderByOffset` support to `ArtifactAdapterRegistry.Inspect(...)` to sort objects by file byte offset for forward-sequential reads.
- Added `ArtifactAdapterRegistry.Inspect(SerializedFile, ...)` overloads to dispatch directly over loose `SerializedFile` instances without an archive container.
- `TypeTree.UnsupportedManagedReferenceShapeException`: typed exception for unsupported `[SerializeReference]` shapes.
- `ObjectRef.Snapshot()` / `SerializedFile.TryGetSnapshot()`: caches an object's decoded fields (`ObjectSnapshot`/`SnapshotField`) keyed by PathId, so repeated lookups of the same object -- e.g. several sibling components resolving their owning GameObject's name -- pay one type-tree walk instead of one per lookup. Fields larger than `MaterializeOptions.MaxInlineFieldSizeBytes` (1024 bytes by default) stay deferred/lazy, same as `TypeTreeReader` already makes them.
- `SerializedFile.MaterializeAll(options)`: eagerly snapshots every (optionally `TypeIdFilter`ed) object, sorted by byte offset, for a consumer that knows upfront it will touch most/all objects in a file. Opt-in only -- opening a file never materializes anything automatically.
- `PPtr.TryResolveSnapshot()`: resolves a local reference straight to a cached `ObjectSnapshot`.

### Fixed

- Deeply nested type trees (>64 levels) now throw instead of risking a stack overflow.
- `ArtifactArchive` now detects and works around a native mount-path resolution quirk affecting some Unity Editor builds.
- `TypeTreeCache` no longer re-walks a full native type tree per object; objects of the same ClassID (every type except MonoBehaviour, whose type tree varies per script) now share one cached walk, eliminating an O(object count) native-call cost that dominated large-scene scans.

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
