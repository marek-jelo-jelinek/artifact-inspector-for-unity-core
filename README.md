# Tesearis.ArtifactInspectorForUnity.Core

Typed, lazy access to the objects inside Unity's built artifacts (player builds or asset bundles) from inside the Unity Editor or as a standalone .NET
library.

## What it does

Tesearis.ArtifactInspectorForUnity.Core mounts a built archive using Unity's own `UnityFileSystemApi` and exposes the serialized objects inside it. Each object's
fields are described by its type tree and read lazily through a `TypeTreeReader`: nothing is bulk-decoded or copied until you actually ask for a
field's value or raw bytes, so inspecting a bundle full of large textures or meshes doesn't require loading them all into memory first.

Typed, tolerant views of specific known object types can be layered on top via the public `ArtifactAdapter<T>` mechanism (see
[Custom adapters](#custom-adapters)).

This is the core library: a plain .NET package with no compile-time dependency on Unity. Pairs with the separate
`Tesearis.ArtifactInspectorForUnity.Editor` package (adapters for `UnityEngine.*` types, etc., see the
[artifact-inspector-for-unity-editor](https://github.com/marek-jelo-jelinek/artifact-inspector-for-unity-editor) repository).

## Requirements

- Targets Unity **6000.3+** only: bundles/player builds must be built by Unity 6000.3 or newer, and the `UnityFileSystemApi`
  library loaded at runtime must come from a matching Unity Editor install.
- Targets `netstandard2.0` and `net8.0`. No compile-time dependency on Unity.
- At runtime, needs a local Unity Editor installation to load `UnityFileSystemApi` (`.dylib` / `.dll` / `.so`) from. Auto-discovered when running
  inside the Editor process; outside it, point at the library explicitly (see [Running outside the Editor](#running-outside-the-editor)).
- `UnityFileSystemApi` is an internal, undocumented Unity component: its location, exported function signatures, and behavior aren't guaranteed across
  Editor versions, and may change or be removed without notice. Account for that before relying on this in CI or production. `ArtifactInspector.GetNativeLibraryVersion()` /
  `GetUnityEditorVersion()` and `SerializedFile.Version` let you check what you're actually talking to at runtime (e.g. log or assert on them at startup)
  instead of only finding out when something breaks.

## Install

```
dotnet add package Tesearis.ArtifactInspectorForUnity.Core
```

For use from Unity Editor tooling, add the package to an Editor-only assembly (e.g. via [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)
or by vendoring the DLL). It works equally well as a plain dependency of a standalone .NET tool, as long as `ArtifactInspector.SetupLibraryPath` is
configured.

## Quick start

```csharp
using Tesearis.ArtifactInspectorForUnity.Core;

using var archive = ArtifactInspector.OpenAssetBundle("myasset.bundle");

foreach (var entryName in archive.EntryNames)
{
    using var serializedFile = archive.OpenSerializedFile(entryName);

    foreach (var objectRef in serializedFile.Objects)
    {
        if (objectRef.ClassName != "Texture2D") continue;

        var reader = objectRef.GetReader();
        var name = reader.Field("m_Name").AsString();
        var width = reader.Field("m_Width").AsInt32();
        var height = reader.Field("m_Height").AsInt32();
        Console.WriteLine($"{name}: {width}x{height}");
    }
}
```

Fields can be nested or array-valued:

```csharp
var reader = objectRef.GetReader();

if (reader.TryGetField("m_Channels", out var channels))
{
    foreach (var channel in channels.Elements())
    {
        var format = channel.Field("format").AsByte();
    }
}
```

## Custom adapters

Adapters turn a raw type-tree reader into a typed, tolerant view of a known object type, via the public `ArtifactAdapter<T>` mechanism. Third
parties, including Unity Editor-side code living in a separate assembly (where real `UnityEngine.*` types are available), can register their own
adapters and dispatch across all of them with `ArtifactAdapterRegistry.Inspect`:

```csharp
using Tesearis.ArtifactInspectorForUnity.Core;
using Tesearis.ArtifactInspectorForUnity.Core.Adapters;

var registry = new ArtifactAdapterRegistry()
    .Register(new AudioClipAdapter())
    .Register(new MonoBehaviourAdapter());

using var archive = ArtifactInspector.OpenAssetBundle("myasset.bundle");

foreach (var obj in registry.Inspect(archive))
{
    switch (obj)
    {
        case AudioClipInfo clip:
            Console.WriteLine($"{clip.Name}: {clip.Channels} channel(s)");
            break;
        case RawObject raw:
            Console.WriteLine($"(unhandled) {raw.ClassName} #{raw.ObjectRef.PathId}");
            break;
    }
}
```

(`AudioClipAdapter` and `MonoBehaviourAdapter` are illustrative here - write your own following [Writing an adapter](#writing-an-adapter).)

Objects that no registered adapter recognizes come back as a `RawObject` (its `ClassName` plus the original `ObjectRef`) instead of being silently
dropped, so you can log them or fall back to `raw.ObjectRef.GetReader()` yourself.

`Inspect` is a convenience on top of the manual loop from the quick start above: `SerializedFile.Objects`/`ObjectRef.GetReader()` are still there for
one-off or lower-level use, and dispatching through a registry additionally reuses a single `TypeTreeReader` per object across matching and reading,
instead of building one for `ClassName` and another for `GetReader()`.

### Writing an adapter

Implement `ArtifactAdapter<T>`:

```csharp
public sealed class AudioClipAdapter : ArtifactAdapter<AudioClipInfo>
{
    protected override string ClassName => "AudioClip";

    public override AudioClipInfo Read(ArtifactAdapterContext context)
    {
        var reader = context.Reader; // the same TypeTreeReader ObjectRef.GetReader() would give you
        // ... decode fields, optionally using context.Archive / context.SerializedFile for streamed
        // data or cross-references to other objects in the same file ...
    }
}
```

- Override `Matches` instead of `ClassName` for anything beyond a straight type-name check, e.g. only handling a `MonoBehaviour` whose
  `m_Script` field (`.AsPPtr()`) resolves to a particular script.
- `context.SerializedFile.TryGetObject(pathId, out var objectRef)` lets an adapter look up another object in the same file by path ID (PPtr
  resolution).
- `context.Archive` lets an adapter read from other archive entries, e.g. to resolve a streamed resource file named by a field in the object.
- **Adapters must not retain `context.Reader`, `context.SerializedFile`, or `context.Archive` past `Read` returning**: copy whatever data you need
  into your own result type first. `Inspect` disposes each `SerializedFile` as it moves on to the next archive entry.
- Implement `IArtifactAdapter` directly instead of `ArtifactAdapter<T>` only if you need a `struct` result type, or want to skip the
  `ClassName`-based default `Matches`.

Registries are ordered, first-match-wins: register more specific adapters before more general ones. `new ArtifactAdapterRegistry()` always
starts empty; this library bundles no adapters of its own.

## Public API

Quick reference; see the examples above for usage, and each type's XML doc comments for full member-level detail.

- `ArtifactInspector`: entry point (`OpenAssetBundle`, `SetupLibraryPath`, `AddTypeTreeSource`/`RemoveTypeTreeSource`, native/editor version checks).
- `ArtifactArchive`, `SerializedFile`, `ObjectRef`, `ExternalReference`, `PPtr`, `ArchiveEntryInfo`, `IRandomAccessByteSource`, `GuidFormatting`: the archive and serialized file model.
- `TypeTreeReader`, `TypeTreeNode`, `TypeTreeSummary`: lazy, random access field reading.
- `ArtifactAdapterRegistry`, `IArtifactAdapter`, `ArtifactAdapter<T>`, `ArtifactAdapterContext`, `RawObject`, `StreamingInfo`: the adapter mechanism, see [Custom adapters](#custom-adapters) above.
- `BinaryFormat.SerializedFileDetector`, `SerializedFileInfo`, `StrippedObjectInfo`, `TypeIdRegistry`, `YamlSerializedFileDetector`: stripped file (no TypeTree) support, see [Stripped files](#stripped-files-no-typetree) below.
- `ArtifactInspectorException`, `NativeCallException`, `SerializedFileOpenException`, `NativeFeatureNotSupportedException`: the exception hierarchy, see [Exceptions](#exceptions) below.

### Stripped files (no TypeTree)

Shipped Player builds are normally built with `EnableTypeTree=false`, which `UFS_OpenSerializedFile` refuses to open at all. `BinaryFormat.SerializedFileDetector` reads a SerializedFile's header, object list, and external references directly off bytes instead, with no native call and no TypeTree
needed for any of that. It works over the same `IRandomAccessByteSource` this library already uses internally, so it runs equally well against a
bare on-disk file and an archive-mounted entry:

```csharp
using Tesearis.ArtifactInspectorForUnity.Core.BinaryFormat;

// A bare file on disk, no archive, no Unity Editor process needed:
if (SerializedFileDetector.TryDetect("Builds/StandaloneWindows64/mybundle_Data/sharedassets0.assets", out var info))
{
    Console.WriteLine($"Unity {info.UnityVersion}, EnableTypeTree={info.EnableTypeTree}");
    foreach (var obj in info.Objects)
    {
        Console.WriteLine($"{obj.PathId}: {obj.ClassName} ({obj.ByteSize} bytes)");
    }
}
```

Or against an archive-mounted entry, including ones the native API can't open:

```csharp
using var archive = ArtifactInspector.OpenAssetBundle("Builds/StandaloneWindows64/mybundle");
var byteSource = archive.OpenRawByteSource(archive.EntryNames[0]);
SerializedFileDetector.TryDetect(byteSource, out var info);
```

Field *values* are out of scope for a stripped file: there is no schema to read them from. What's available is the object list (id/offset/size,
labeled by class name via `TypeIdRegistry`) and the external-reference list, both of which don't actually require a TypeTree to read.

- `BinaryFormat.SerializedFileDetector`: `TryDetect` detects and parses a SerializedFile straight off bytes; `IsMissingTypeTrees` is a cheap fast path that checks only whether `EnableTypeTree` is false.
- `SerializedFileInfo` (readonly struct): the parsed header, metadata, object list, and external references, plus `MetadataParsed`/`MetadataParseError` for versions this library can't fully parse (only format versions 22 and 23, Unity 6000.3.x, are supported).
- `StrippedObjectInfo` (readonly struct): `PathId`, `TypeId`, `ByteOffset`, `ByteSize`, `ClassName`; deliberately separate from `ObjectRef` since there's no TypeTree to build a reader from, so it exposes no `GetReader()`. `Name` is also populated best-effort, with no TypeTree, for classes where `m_Name`'s byte position is known without one: direct `NamedObject` subtypes (`Texture2D`, `Mesh`, `AudioClip`, `Material`, ...), where it's reliably the object's first field, and `MonoBehaviour`, at a fixed offset past its `PPtr`/`enabled` prefix (see `BinaryFormat.StrippedObjectNameReader`). `null` for every other class, and for a read that didn't look like a valid name -- this is deliberately not a general field decoder.
- `TypeIdRegistry`: static Unity ClassID to class name lookup, the only way to label a stripped object's type.
- `YamlSerializedFileDetector`: sniffs the `%YAML 1.1` magic to reject Editor-text-format `.asset`/`.prefab`/`.unity` files up front, as a fast triage before attempting a binary parse.

`ArtifactArchive.OpenSerializedFile` also uses this detector internally: when the native open fails and the entry's bytes positively confirm it has
no TypeTrees, it throws `SerializedFileOpenException` instead of the generic `NativeCallException`: see [Exceptions](#exceptions).

### Running outside the Editor

Inside the Editor process, the native `UnityFileSystemApi` library is located automatically from the running instance (via
`EditorApplication.applicationPath`) and calls are made against that instance's own bundled copy, with no setup call needed.

Outside the Editor (a standalone tool, a CI job, a test runner) there is no running Editor process to auto-detect one from. In that case,
`ArtifactInspector.SetupLibraryPath(path)` **must** be called once, before any other call into the library, otherwise those calls fail.

```csharp
using Tesearis.ArtifactInspectorForUnity.Core;

// Pass exact location of UnityFileSystemApi (.dylib / .dll / .so).
ArtifactInspector.SetupLibraryPath("/path/to/UnityFileSystemApi.dylib");

using var archive = ArtifactInspector.OpenAssetBundle("Builds/StandaloneWindows64/mybundle");
```

- `ArtifactInspector.SetupLibraryPath(string path)`: points the library at an explicit `UnityFileSystemApi` native library path, for use when there is no running Editor process to auto-detect one from.

Newer SerializedFile formats (version ≥ 23) can have their TypeTree blobs extracted out-of-band at build time instead of stored inline; opening one
of those requires registering that external source first, via `ArtifactInspector.AddTypeTreeSource(path)`. Like `SetupLibraryPath`, this is a
process-wide setup step, called once before opening a file that needs it, not per-archive/per-file. It requires a native library new enough to
support it (Unity 6.5+); on an older library it throws `NativeFeatureNotSupportedException` instead of failing silently or crashing.

```csharp
ArtifactInspector.AddTypeTreeSource("/path/to/extracted/typetree.dat");

using var archive = ArtifactInspector.OpenAssetBundle("Builds/StandaloneWindows64/mybundle");
```

- `ArtifactInspector.AddTypeTreeSource(string filePath)` / `RemoveTypeTreeSource(string filePath)`: register or unregister an out-of-band TypeTree source, process-wide.

### Exceptions

- `ArtifactInspectorException`: base type for exceptions raised by this library.
- `NativeCallException : ArtifactInspectorException`: a call into the native `UnityFileSystemApi` failed; carries the underlying `ReturnCode`.
- `SerializedFileOpenException : ArtifactInspectorException`: thrown by `ArtifactArchive.OpenSerializedFile` in place of `NativeCallException`
  specifically when the failure is positively confirmed (via `SerializedFileDetector.IsMissingTypeTrees`) to be caused by the entry having no
  TypeTrees; carries `EntryName` and `MissingTypeTrees`. Every other `OpenSerializedFile` failure mode still throws `NativeCallException` unchanged.
- `NativeFeatureNotSupportedException : ArtifactInspectorException`: thrown in place of `NativeCallException` when a call requires a native entry
  point that isn't present at all in the loaded `UnityFileSystemApi` library (e.g. `AddTypeTreeSource`/`RemoveTypeTreeSource` against a native
  library older than Unity 6.5, or `SerializedFile.TypeTrees`/`GetTypeTreeByIndex` against a native library that doesn't export
  `UFS_GetTypeTreeCount`/`UFS_GetTypeTreeInfo`/`UFS_GetTypeTreeByIndex`); carries `SymbolName`.

## Resource management

`ArtifactArchive` and `SerializedFile` both implement `IDisposable`. Dispose in nested order (innermost first), as in the quick start example above.
Disposing an archive also actively invalidates any `SerializedFile`s still open from it: any further call on one throws `ObjectDisposedException`,
the same as if you'd disposed that `SerializedFile` directly, rather than leaving it looking usable while its native calls silently target a mount
that no longer exists.

## Testing

`dotnet test` runs the full suite, but the integration tests under `tests/Tesearis.ArtifactInspectorForUnity.Core.Tests/Native/Interop/` that exercise the real
native `UnityFileSystemApi` library are skipped automatically unless the binary for the current OS is present.

To run them against a specific Unity Editor version, copy that Editor install's `UnityFileSystemApi.dylib` (macOS) / `.dll` (Windows) / `.so`
(Linux) into `tests/Tesearis.ArtifactInspectorForUnity.Core.Tests/UnityFileSystemApiLibraries/`. These are Unity's own undocumented native components and must not
be redistributed.

The tests under `tests/Tesearis.ArtifactInspectorForUnity.Core.Tests/Model/RealTestDataIntegrationTests.cs` go one step further and exercise the full pipeline,
mounting an archive, walking its objects, and dispatching them through the adapter registry, against real Unity-built output. They're skipped the
same way unless there's also something to run them against: drop any real file (an asset bundle, a player build's data file, ...) into
`tests/Tesearis.ArtifactInspectorForUnity.Core.Tests/TestData/`. No particular name, extension, or content is expected: every file found there is exercised, and
the assertions are structural (it opens, every object's fields are readable, nothing is silently dropped) rather than tied to specific expected
values, so any real data works. Neither of these folders' contents are committed (see their `.gitkeep`s) or redistributed.

## Known limitations

A few boundaries are intentional, not oversights:

- **`[SerializeReference]` fields aren't decoded.** Unity's polymorphic managed-reference shapes aren't readable via
  the type-tree walk this library uses; `TypeTreeNode` deliberately throws `ArtifactInspectorException` rather than
  silently misreading them.
- **No WebGL bundle support.** Unity WebGL's separate `UnityWebData1.0` container format isn't handled; this is new
  scope, not a gap in existing functionality.

See [CHANGELOG.md](CHANGELOG.md) for what's included in each release.

## Acknowledgments

This library's approach to the `UnityFileSystemApi` native binding and type-tree reading was informed by studying
[UnityDataTools](https://github.com/Unity-Technologies/UnityDataTools) as a reference for the shape of Unity's undocumented native ABI and
serialized-file layout, then independently written from scratch. One current exception: `BinaryFormat/TypeIdRegistry.cs`'s ClassID lookup
table is sourced from UnityDataTools' own generated table and used here under its license rather than this project's own. See
[ThirdPartyNotices.md](ThirdPartyNotices.md) for the full breakdown.

"Unity" and "Unity Technologies" are trademarks of Unity Technologies. This project is not affiliated with, endorsed by, or sponsored by
Unity Technologies.

## License

MIT, see [LICENSE](LICENSE), with exceptions noted in [ThirdPartyNotices.md](ThirdPartyNotices.md).
