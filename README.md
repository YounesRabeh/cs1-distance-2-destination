<a href="https://github.com/YounesRabeh/cs1-distance-2-destination"><img src="assets/Distance2Destination-thumbnail-text-only.png" alt="Distance 2 Destination" width="100%"></a>

<div align="center">

  <p align="center">
    <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/badge/Platform-Cities%3A_Skylines_1-1B2838?style=for-the-badge&amp;logo=steam&amp;logoColor=white" alt="Platform: Cities: Skylines 1"></a>
    <a href="https://dotnet.microsoft.com/en-us/download/dotnet-framework/net35-sp1"><img src="https://img.shields.io/badge/.NET_Framework-3.5-512BD4?style=for-the-badge&amp;logo=dotnet&amp;logoColor=white" alt=".NET Framework 3.5"></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402"><img src="https://img.shields.io/badge/Harmony-2.2.2--0-FF6B6B?style=for-the-badge" alt="Harmony 2.2.2-0 mod dependency"></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3789370121"><img src="https://img.shields.io/badge/Steam_Workshop-Install-2EA44F?style=for-the-badge&amp;logo=steam&amp;logoColor=white" alt="Install from Steam Workshop"></a>
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-2EA44F?style=for-the-badge&amp;logo=opensourceinitiative&amp;logoColor=white" alt="MIT License"></a>
    <a href="docs/README.md"><img src="https://img.shields.io/badge/Documentation%20Hub-0969DA?style=for-the-badge&amp;logo=mdbook&amp;logoColor=white" alt="Open the Distance 2 Destination documentation hub"></a>
  </p>

  <p>Shows the remaining distance along a selected citizen or road vehicle's existing route.</p>

  <p align="center">
    <a href="#features">Features</a> •
    <a href="#showcase">Showcase</a> •
    <a href="#quick-start">Quick Start</a> •
    <a href="#development">Development</a> •
    <a href="#guides">Guides</a>
  </p>
</div>

---

## Features

- 🛣️ Shows the remaining route distance in the normal information window.
- 🚚 Supports service vehicles, other road vehicles, bicycles, and pedestrians.
- 📏 Supports metric and imperial units.
- 🙈 Hides the field when there is no useful active trip to display.
- 🔄 Updates automatically as the selected vehicle or citizen moves.

## Showcase

Runtime screenshots are not currently included in the repository.

## Quick start

1. Install Cities: Skylines 1 version 1.21.1-f9 or later.
2. Subscribe to [Harmony 2.2.2-0 (Mod Dependency)](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402); Distance 2 Destination does not include Harmony itself.
3. Install [Distance 2 Destination from the Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3789370121).
4. In **Content Manager > Mods**, enable **Distance 2 Destination v1.1.8**, load a city, and select a moving road vehicle, bicycle, or pedestrian.

### Configuration overview

| Section | Configure | Available options |
| --- | --- | --- |
| `Show distance for` | Choose the entity categories that display distance. | `Service vehicles`, `All other vehicles`, `Pedestrians` |
| `Units` | Choose the distance format. | `Metric (m, km)`, `Imperial (ft, mi)` |

Open **Options > Mods Settings > Distance 2 Destination v1.1.8** to change these settings.

## Development

For the complete setup and local-installation procedure, see [Development and building](docs/Development.md).

| Need | Command |
| --- | --- |
| Restore dependencies | `nuget restore packages.config -PackagesDirectory packages` |
| Build a Release DLL | `msbuild DistanceToDestination.csproj /p:Configuration=Release` |

Before building against a nonstandard game installation, copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and set `CitiesSkylinesManagedDir`. The local props file is ignored by Git.

## Guides

Choose a guide by task, or browse the complete [documentation hub](docs/README.md):

| I want to… | Start here |
| --- | --- |
| Install the mod or change settings | [User guide](docs/UserGuide.md) |
| Restore dependencies, build, or install locally | [Development and building](docs/Development.md) |
| Understand route calculations and UI integration | [Technical design](docs/TechnicalDesign.md) |
| Run the recommended checks | [Verification checklist](docs/Verification.md) |

## Tech stack

<p align="left">
  <a href="https://learn.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/C%23-Language-239120?style=for-the-badge&amp;logo=csharp&amp;logoColor=white" alt="C#"></a>
  <a href="https://dotnet.microsoft.com/en-us/download/dotnet-framework/net35-sp1"><img src="https://img.shields.io/badge/.NET_Framework-3.5-512BD4?style=for-the-badge&amp;logo=dotnet&amp;logoColor=white" alt=".NET Framework 3.5"></a>
  <a href="https://www.nuget.org/packages/CitiesHarmony.API/2.2.0"><img src="https://img.shields.io/badge/CitiesHarmony.API-2.2.0-FF6B6B?style=for-the-badge&amp;logo=nuget&amp;logoColor=white" alt="CitiesHarmony.API 2.2.0"></a>
</p>

---

## License

Distributed under the [MIT License](LICENSE).
