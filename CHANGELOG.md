# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed

- **Breaking:** `SerializedFileDetector.IsMissingTypeTrees` / `YamlSerializedFileDetector.IsYamlSerializedFile` now throw on invalid paths instead of returning `false`.
- **Breaking:** `ExternalReferenceType` moved from `Native` to `Model` namespace.
- `TypeTreeNode` no longer throws eagerly for unsupported `[SerializeReference]` shapes; the error is now deferred until that field is read.

### Added

- `TypeTree.UnsupportedManagedReferenceShapeException`: typed exception for unsupported `[SerializeReference]` shapes.

### Fixed

- Deeply nested type trees (>64 levels) now throw instead of risking a stack overflow.
- `ArtifactArchive` now detects and works around a native mount-path resolution quirk affecting some Unity Editor builds.

### Known issues (not yet resolved)

- Reading field-level data through an archive-mounted entry -- `ArtifactArchive.ReadRawEntry`/`OpenRawByteSource`, or walking a
  `TypeTreeReader`'s fields (`HasField`/`Field`) -- can crash the host process (a native segfault) for some entries, outside a running Unity
  Editor (confirmed via a standalone .NET host against real Addressables/AssetBundle output). Doesn't reproduce for a non-archived file, and
  isn't specific to one class. Listing objects (`SerializedFile.Objects`/`ObjectRef` metadata) doesn't hit this path and hasn't crashed. Needs
  verification from inside an actual Editor process before this is understood, let alone fixed.

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
