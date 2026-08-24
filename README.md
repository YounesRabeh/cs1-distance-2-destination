# Route Distance

Route Distance is a read-only Cities: Skylines 1 mod intended to show the remaining distance along a selected road vehicle or moving citizen's existing route.

## v1 behavior

The v1 implementation covers the complete plan through Phase 23:

- buildable ICities project bootstrap
- inspection of the installed game assemblies
- defensive selected-entity and existing-path validation
- bounded, cycle-safe traversal of remaining `PathUnit` positions
- physical distance over each remaining lane portion
- lane-transition distance without double-counting either lane
- current vehicle/citizen progress projected onto the active lane or transition
- a vanilla-styled field in vehicle and citizen world-info panels
- reroute-safe refreshes every 0.75 seconds while a supported panel is visible
- safe unavailable output (`Distance to destination: —`) for missing or transient paths
- reversible, owner-scoped Harmony patches

Select a spawned road vehicle or moving pedestrian to see its remaining route distance. Citizens entering or riding a vehicle intentionally show `—`; v1 does not reconstruct multimodal journeys.

## In-game settings

Open **Options > Mods Settings > Route Distance v1.1.0** to configure the display. The visibility checkboxes are independent and enabled by default:

- **Service vehicles** controls vanilla city-service vehicle panels.
- **All other vehicles** controls private, cargo, public-transport, and other road-vehicle panels.
- **Pedestrians** controls moving citizen panels.

The **Distance units** dropdown selects **Metric (m, km)** or **Imperial (ft, mi)**. Metric is the default. Changes are persisted by Cities: Skylines and apply on the next normal panel refresh.

The implementation targets and compiles against the installed Cities: Skylines version **1.21.1-f9**. See [docs/Investigation.md](docs/Investigation.md) for the exact findings and distance model.

## Requirements

- Cities: Skylines 1 game assemblies from a local installation
- a .NET Framework/Mono C# build toolchain compatible with .NET 3.5
- [Harmony 2.2.2-0 (Mod Dependency)](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402)
- CitiesHarmony.API 2.2.0, restored from `packages.config`

Game and the CitiesHarmony implementation DLL are referenced with `Private=False`; they are never copied into the mod output. `CitiesHarmony.API.dll` is intentionally copied beside `RouteDistance.dll`, following CitiesHarmony's supported integration model. The dependency release is Harmony **2.2.2-0**. Its installed implementation DLL retains the separate managed assembly version `2.0.4.0`; that internal value is not the mod release version.

## Build configuration

Common Steam locations are detected automatically. For another location, copy:

```text
Directory.Build.user.props.example
```

to:

```text
Directory.Build.user.props
```

and set `CitiesSkylinesManagedDir`. The local props file is ignored by Git. Optional properties are `HarmonyDllPath`, `CitiesHarmonyApiDllPath`, `BuildOutputRoot`, and `ModOutputDir`.

Restore the official API package:

```text
nuget restore packages.config -PackagesDirectory packages
```

Build with a compatible MSBuild implementation:

```text
msbuild RouteDistance.csproj /p:Configuration=Release
```

The build fails early with a specific message when a required game or Harmony dependency is missing. If `ModOutputDir` is configured, `RouteDistance.dll`, `CitiesHarmony.API.dll`, and available symbols are copied there after a successful build.

## Verification status

The release build has been checked against the installed **1.21.1-f9** assemblies. Automated checks cover formatting boundaries, assembly loading, exact panel patch targets, absence of pathfinding calls, absence of writes to game-owned simulation fields, output contents, and source-tree safety/performance rules.

The visual and behavioral criteria—actual panel layout, decreasing values on live routes, rerouting, arrival, and citizen transport transitions—still require the Phase 19-20 in-game matrix. They cannot be truthfully certified by a compiler or static assembly inspection alone. The complete status is recorded in [docs/Investigation.md](docs/Investigation.md#v1-acceptance-verification).

## Safety boundary

The current source only reads the selected entity's existing `m_path`. It does not call `CreatePath`, start pathfinding, alter path units, add path references, scan the city, or write to simulation-owned vehicle, citizen, path, segment, or lane state.

Lane portions are measured by sampling vanilla's `NetLane` Bezier geometry; no fixed distance per path position is assumed. Different-lane transitions use a sampled, tangent-aligned cubic connector between vanilla-derived lane endpoints because the exact connector curve is constructed differently for each AI.

## License

Route Distance source is licensed under the MIT License. Cities: Skylines, CitiesHarmony, Harmony, and their assemblies/assets are not redistributed or relicensed by this repository.
