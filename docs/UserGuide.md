# User guide

## Installation

1. Install Cities: Skylines 1.
2. Subscribe to **Harmony 2.2.2-0 (Mod Dependency)** on Steam Workshop.
3. Install Route Distance.
4. Open **Content Manager > Mods** and enable **Route Distance v1.1.0**.
5. Restart the game after replacing a local build of the mod.

Route Distance includes the small CitiesHarmony API helper, but it does not include the Harmony implementation. Harmony must be installed separately through its Workshop item.

## Showing a distance

Load a city and select a moving road vehicle or pedestrian. When the selected entity has an active route, its information window displays:

```text
Distance to destination: 500 m
```

The displayed value updates while the entity moves. Values are rounded upward to remain readable. A dash means the game temporarily has no stable route available, which can happen during rerouting or arrival.

The field is hidden for idle citizens without a **Going...** activity and for stationary parked vehicles.

## Settings

Open **Options > Mods Settings > Route Distance v1.1.0**.

The **Show distance for** section contains three independent options, all enabled by default:

- **Service vehicles**
- **All other vehicles**
- **Pedestrians**

The **Units** section offers:

- **Metric (m, km)**, the default
- **Imperial (ft, mi)**

Changes are saved by the game and apply on the next information-panel refresh.

## Troubleshooting

### The mod does not appear

Confirm that `RouteDistance.dll` is inside a Route Distance folder under the game's local `Addons/Mods` directory. Restart the game after installing or replacing the file.

### The mod appears but no distance is shown

Check that Route Distance and Harmony are enabled in Content Manager. Also check the Route Distance visibility settings for the selected entity type.

The field is intentionally absent when the selected entity has no supported active trip. Near arrival, a temporary dash can appear while the game releases or replaces the route.

### The value changes in large steps

This is display rounding. The internal calculation uses the route geometry, while the information window presents readable rounded values.
