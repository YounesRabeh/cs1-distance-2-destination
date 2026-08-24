# Route Distance

Route Distance is a read-only Cities: Skylines 1 mod intended to show the remaining distance along a selected road vehicle or moving citizen's existing route.

## Current implementation boundary

This repository currently implements the requested plan through **Phase 4**:

- buildable ICities project bootstrap
- inspection of the installed game assemblies
- defensive selected-entity and existing-path validation
- bounded, cycle-safe traversal of remaining `PathUnit` positions

Physical lane-distance accumulation starts in Phase 5, and the vanilla label/Harmony lifecycle starts in Phases 10-15. Consequently this checkpoint does **not** yet display a distance in game, and the public `TryGet...RemainingDistance` methods correctly return `false` rather than fabricate a value.

The local game installation inspected during development is **1.21.1-f9**, while the design target is 1.17.x. See [docs/Investigation.md](docs/Investigation.md) for the exact findings and compatibility warning.

## Requirements

- Cities: Skylines 1 game assemblies from a local installation
- a .NET Framework/Mono C# build toolchain compatible with .NET 3.5
- CitiesHarmony for the later runtime integration phase

Game and CitiesHarmony DLLs are referenced with `Private=False`; they are never copied into the mod output.

## Build configuration

Common Steam locations are detected automatically. For another location, copy:

```text
Directory.Build.user.props.example
```

to:

```text
Directory.Build.user.props
```

and set `CitiesSkylinesManagedDir`. The local props file is ignored by Git. Optional properties are `HarmonyDllPath`, `BuildOutputRoot`, and `ModOutputDir`.

Build with a compatible MSBuild implementation:

```text
msbuild RouteDistance.csproj /p:Configuration=Release
```

The build fails early with a specific message when a required installed game assembly is missing. If `ModOutputDir` is configured, the compiled DLL and available symbols are copied there after a successful build.

## Safety boundary

The current source only reads the selected entity's existing `m_path`. It does not call `CreatePath`, start pathfinding, alter path units, add path references, scan the city, or write to simulation-owned vehicle, citizen, path, segment, or lane state.

## License

Route Distance source is licensed under the MIT License. Cities: Skylines, CitiesHarmony, Harmony, and their assemblies/assets are not redistributed or relicensed by this repository.
