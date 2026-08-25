# Development and building

## Target environment

- Cities: Skylines 1.21.1-f9
- .NET Framework 3.5 compatible compiler
- CitiesHarmony.API 2.2.0
- Harmony 2.2.2-0 (Mod Dependency)

The project references assemblies from an installed copy of Cities: Skylines. It does not redistribute game assemblies or the Harmony implementation.

## Project layout

```text
Distance/       Existing-path traversal and geometry
Patches/        Vehicle and citizen information-panel patches
UI/             Distance row and panel layout
assets/         Full-size artwork and optimized Workshop preview
Mod.cs          Mod metadata and settings UI
ModSettings.cs  Persistent preferences
Patcher.cs      Harmony lifecycle
```

## Restore dependencies

```text
nuget restore packages.config -PackagesDirectory packages
```

For a nonstandard game installation, copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and set `CitiesSkylinesManagedDir`. The local props file is ignored by Git.

## Build

```text
msbuild DistanceToDestination.csproj /p:Configuration=Release
```

The default output is:

```text
artifacts/bin/Release
```

The output contains `DistanceToDestination.dll`, symbols when available, and `CitiesHarmony.API.dll`. It must not contain game assemblies or `CitiesHarmony.Harmony.dll`.

The local build-and-install script also creates a versioned Workshop upload folder and ZIP archive under `artifacts/workshop`, for example `DistanceToDestination-v1.1.3` and `DistanceToDestination-v1.1.3.zip`. The DLL names inside remain unversioned because the game expects stable assembly names.

## Local build and installation

This workspace includes the ignored helper script:

```text
./build-and-install-local.sh
```

It builds with the game's bundled Mono and updates the local DistanceToDestination mod folder. Its paths are specific to the local development machine and should be adjusted for another installation.

Restart Cities: Skylines after updating the installed DLL because an already running game keeps the previous assembly loaded.

## Build warning

The official CitiesHarmony API can produce an ICities 1.16/1.17 assembly-unification warning when compiled against the installed game. The verified Release build resolves it to the installed ICities 1.17 assembly.
