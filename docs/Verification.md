# Verification checklist

## Automated and static checks

The current Release build has been checked for:

- Successful compilation against Cities: Skylines 1.21.1-f9
- CitiesHarmony.API 2.2.0 integration with Harmony 2.2.2-0
- Exact vehicle and citizen panel patch targets
- Owner-scoped patch removal
- Bounded path traversal and cycle detection
- No calls that create paths or start pathfinding
- No writes to vehicle, citizen, path, segment, or lane simulation fields
- Packaging without game assemblies or the Harmony implementation
- Matching built and locally installed `DistanceToDestination.dll` artifacts

The official API produces one expected ICities 1.16/1.17 unification warning; the build resolves it to the installed ICities 1.17 assembly.

## Recommended in-game checks

After each UI or geometry change, test:

- Private, cargo, public-transport, and city-service vehicles
- Walking citizens with active paths in English and non-English game languages
- Idle or pathless citizens and citizens riding in vehicles
- Parked, parking, and pathless road vehicles
- Short and long routes
- Curved roads, ramps, bridges, and wide intersections
- Mid-route selection and rerouting
- Arrival and temporary path release
- Metric and imperial formatting
- Every visibility setting, including persistence after restart
- Panel layout at different UI scales
- Mod disable/re-enable cleanup

## Expected behavior

- Only one distance row is present.
- The panel background extends below the final line.
- Values generally decrease along an unchanged route.
- Rerouting replaces the value safely.
- A temporary read failure on a nonzero active route displays a dash.
- Entities without an active path do not display a distance row.
- Unsupported or disabled categories display no distance row.
- Closing the panel stops refresh work.
