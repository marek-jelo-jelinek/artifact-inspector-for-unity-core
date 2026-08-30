# Third-party notices

This project is licensed under the MIT license (see [LICENSE](LICENSE)). Where this project incorporates
third-party code, data, or assets under different terms, that is disclosed below, file by file -- most
of this codebase is independently written and covered by the MIT license above.

## UnityDataTools

[UnityDataTools](https://github.com/Unity-Technologies/UnityDataTools) is Unity Technologies' own reference tool
for inspecting built Unity artifacts, licensed under the
[Unity Companion License](https://unity3d.com/legal/licenses/unity_companion_license) (UCL) -- not MIT or another
general-purpose open-source license.

Most of this library's approach to Unity's undocumented `UnityFileSystemApi` native ABI and SerializedFile binary
layout was informed by studying UnityDataTools as a reference for the *shape* of that undocumented surface, then
independently written from scratch: no code, comments, or data from UnityDataTools is included in
`Native/Interop/UnityFileSystemApi.cs`, the `BinaryFormat/SerializedFileHeaderParser.cs` /
`SerializedFileDetector.cs` parsing, `TypeTree/`, or the `Adapters`/`Model` layers.

Specifically:

- **`BinaryFormat/TypeIdRegistry.cs`**: the ClassID-to-class-name lookup table (and one doc comment) is sourced
  from UnityDataTools' own `TypeIdRegistry`, which Unity generates directly from their internal engine type list --
  it includes hashed IDs for newer built-in types (e.g. `SpriteAtlas`, `Tilemap`, `TerrainLayer`,
  `ArticulationBody`) that aren't listed on Unity's public
  [ClassIDReference](https://docs.unity3d.com/Manual/ClassIDReference.html) manual page. This table is used here
  under the terms of the Unity Companion License rather than under this project's own MIT license; it is not
  independently authored the way the rest of this codebase is.
- The `ReturnCode` enum (`Native/ReturnCode.cs`) and the `TypeTreeFlags` / `TypeTreeMetaFlags` / `TypeTreeCategory`
  enums (`Native/`) use the same member names, in the same order, as UnityDataTools' equivalents for the same
  native concepts. The underlying integer values are dictated by Unity's native ABI and would be identical in any
  correct binding regardless of source; the specific English names were plausibly informed by studying
  UnityDataTools' naming choices. Noted here for transparency; the surrounding implementation (P/Invoke mechanism,
  marshaling, exception handling) is independently written.

## Unity's native `UnityFileSystemApi` library

This library never bundles or redistributes Unity's `UnityFileSystemApi.dylib` / `.dll` / `.so`. It is an internal,
undocumented component of the Unity Editor; users of this library must point it at a copy sourced from their own
licensed Unity Editor installation (see the README's "Running outside the Editor" section).

## Trademarks

"Unity" and "Unity Technologies" are trademarks or registered trademarks of Unity Technologies or its affiliates.
This project is not affiliated with, endorsed by, or sponsored by Unity Technologies.
