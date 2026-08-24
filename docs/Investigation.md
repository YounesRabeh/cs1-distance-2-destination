# Assembly investigation and distance model (Phases 1-7)

## Scope and target version

The inspected files are the assemblies actually installed under:

```text
~/.local/share/Steam/steamapps/common/Cities_Skylines/Cities_Data/Managed
```

`BuildConfig` reports **1.21.1-f9** (`APPLICATION_VERSION_A=1`, `B=21`, `C=1`, build `9`). Per the project target decision, the implementation compiles against and supports this installed assembly layout. The managed-assembly path remains overrideable for other installations, but compatibility with a different game version must be verified separately.

The inspection used the game's bundled Mono.Cecil against `Assembly-CSharp.dll`; it did not use recreated API definitions or third-party documentation.

## World-info panels

### Normal road vehicles

- Exact base panel: `VehicleWorldInfoPanel : WorldInfoPanel`.
- `WorldInfoPanel : ToolsModifierControl` owns protected `InstanceID m_InstanceID` and protected `Vector3 m_WorldMousePosition`.
- `VehicleWorldInfoPanel.Start()` resolves the existing `VehicleName`, `Status`, `Target`, and `Type` controls. `m_Status` is a protected `UILabel`.
- `WorldInfoPanel.SetTarget(Vector3, InstanceID)` stores `m_InstanceID`, validates it, calls virtual `OnSetTarget()`, then calls `LateUpdate()`.
- `VehicleWorldInfoPanel.OnSetTarget()` handles a target change. `VehicleWorldInfoPanel.UpdateBindings()` is the normal binding refresh.
- Private `WorldInfoPanel.LateUpdate()` returns when hidden; while visible it updates the position and calls virtual `UpdateBindings()`.
- `WorldInfoPanel.OnVisibilityChanged()` records/removes the last visible panel. `OnHide()` is an available virtual close hook, and `ClearTarget()` replaces `m_InstanceID` with `InstanceID.Empty`.
- The selected vehicle is `m_InstanceID.Vehicle` (`ushort`) when `m_InstanceID.Type == InstanceType.Vehicle`. Vehicle variants call `Vehicle.GetFirstVehicle()` before presenting the first vehicle of a consist.
- Safe future insertion point: the existing panel root (`WorldInfoPanel.component`). For vanilla-aligned placement, `VehicleWorldInfoPanel.m_Status.parent` is the confirmed existing content container/row parent and `m_Status` is a confirmed style source. No UI integration is implemented through Phase 7.

### Citizens

- Exact panel: `CitizenWorldInfoPanel : HumanWorldInfoPanel : LivingCreatureWorldInfoPanel : WorldInfoPanel`.
- It uses the same `SetTarget` / `OnSetTarget` / visible `LateUpdate` / virtual `UpdateBindings` / `OnHide` lifecycle described above.
- The panel target is a `Citizen` ID (`m_InstanceID.Citizen`, `uint`), not a `CitizenInstance` ID.
- `Citizen.m_instance` (`ushort`) is the active moving instance. A safe conversion is: validate the citizen buffer entry, read `m_instance`, validate the instance buffer entry, and require `CitizenInstance.m_citizen` to point back to the same citizen.
- `Citizen.m_vehicle` (`ushort`) identifies a current vehicle. Vanilla's `HumanAI.SetCurrentVehicle` unspawns the `CitizenInstance`; resident/tourist vehicle spawning transfers the active path/progress to `Vehicle`, then clears `CitizenInstance.m_path`. Passenger-car/taxi parking assigns a path back to the citizen instance. A selected citizen can therefore temporarily have no meaningful walking path while entering or riding a vehicle.
- `HumanWorldInfoPanel` has a protected `UILabel m_Status`; its parent is the confirmed existing content container/style source for a future citizen label. No UI integration is implemented through Phase 7.

## Entity path state and current progress

### Vehicle

- `Vehicle.m_path` is `uint`. `0` is the no-path value: path-start methods release an old nonzero path before assigning a replacement, `VehicleAI.InvalidPath` releases and clears it, and `VehicleManager.ReleaseVehicleImplementation` releases and clears it.
- The path may therefore change or become zero while a panel stays open.
- `Vehicle.m_pathPositionIndex` is a `byte`. Vanilla `PathVisualizer.AddPathsImpl` interprets `255` as position index `0`; otherwise the current `PathUnit` position index is `m_pathPositionIndex >> 1`. An even low bit means movement along the current lane toward that position; an odd low bit means vanilla is processing the connector to the next position.
- `Vehicle.m_lastPathOffset` is a `byte`. When progress begins at the `255` sentinel, `VehicleAI.UpdatePathTargetPositions` sets the index to zero and calls `PathUnit.CalculatePathPositionOffset(index, referencePosition, ref m_lastPathOffset)`. It is the entity's within-lane progress offset associated with the current path position.
- For v1 scope, a supported normal road vehicle can be restricted to created, non-deleted, spawned vehicles whose `VehicleInfo.m_vehicleType` contains `VehicleInfo.VehicleType.Car`.

### CitizenInstance

- `CitizenInstance.m_path` is `uint`, with `0` meaning no active path. `CitizenAI.InvalidPath`, citizen-instance release, vehicle entry/path transfer, and path completion can release/clear it; path finding or leaving a vehicle can replace it.
- Walking citizens use the same chained `PathUnit` representation.
- `CitizenInstance.m_pathPositionIndex` and `m_lastPathOffset` use the same byte semantics as vehicles. `CitizenAI.GetPathTargetPosition` handles `255`, derives the position with `>> 1`, advances the even lane-travel phase, then uses the odd phase for its mode-specific connector curve.
- A supported moving walking instance is created, non-deleted, `OnPath`, not `WaitingPath`, not `EnteringVehicle`, maps bidirectionally to a live `Citizen`, and has `Citizen.m_vehicle == 0`.

## PathUnit layout and lifecycle

- `PathManager.m_pathUnits` is `Array32<PathUnit>`; `PathManager.MAX_PATHUNIT_COUNT` is 262,144.
- `PathUnit.MAX_POSITIONS` is 12. Positions are stored inline as `m_position00` through `m_position11`.
- `m_positionCount` is a byte. Ready path units must have a count from 1 through 12 before position access.
- `GetPosition(int)` returns an inline position for indexes 0-11 and a zero position otherwise. `GetPosition(int, out Position)` additionally checks segment creation/build-version state, but it indexes vanilla buffers before all corruption guards; the mod therefore performs explicit buffer checks.
- `m_nextPathUnit` is a `uint`. Zero terminates the chain. Vanilla path visualization resets the position index to zero for each next unit and follows `m_nextPathUnit` with a hard maximum of `PathManager.MAX_PATHUNIT_COUNT`.
- Route Distance uses the much smaller hard cap of 4,096 units plus cycle detection. A normal route cannot legitimately approach that cap, and it prevents corrupt chains from looping.
- `m_simulationFlags` contains `FLAG_CREATED=1`; released units are reset to zero. `m_referenceCount` is also reset to zero when released.
- `m_pathFindFlags` values are `QUEUED=1`, `CALCULATING=2`, `READY=4`, `FAILED=8`. Traversal requires `READY` and rejects queued, calculating, or failed roots.
- `Position` consists of `ushort m_segment`, `byte m_offset`, and `byte m_lane`.
- `SetPosition` and other mutators exist, but Route Distance never calls them.

### What `PathUnit.m_length` means

`m_length` is not a dependable physical remaining-route distance:

1. `PathManager.CreatePath(..., float maxLength, ...)` initially writes its `maxLength` argument into `PathUnit.m_length`.
2. `PathFind.PathFindImplementation` reads it into `PathFind.m_maxLength`, so its initial role is a search bound.
3. When a path is completed, path finding writes either `BufferItem.m_methodDistance` (pedestrian-only lane type) or `BufferItem.m_duration` into the field.

It is therefore mode/cost dependent. It must not replace lane traversal for Route Distance.

## Position to lane and physical lane geometry

- Canonical resolver: public static `uint PathManager.GetLaneID(PathUnit.Position)`.
- It starts at `NetSegment.m_lanes` and follows `NetLane.m_nextLane` `Position.m_lane` times. Because vanilla assumes valid buffers, Route Distance first checks segment ID/flags and that `m_lane` is below `segment.Info.m_lanes.Length`, then validates the returned lane ID and `NetLane.m_segment` backlink.
- `PathManager.CalculatePosition(Position)` resolves the same lane and passes `m_offset * 0.003921569f` to `NetLane.CalculatePosition`. The byte offset is therefore normalized over 0-255.
- `NetLane` contains its geometric `Bezier3 m_bezier` and cached `float m_length`. `NetLane.UpdateLength()` derives and stores the length from the curve approximation and returns it. `NetLane.m_length` is the confirmed vanilla physical lane-length source for Phase 5.

## Physical remaining-distance model (Phases 5-7)

The calculator never treats positions as equally spaced and never uses `PathUnit.m_length` as meters.

- Two positions on the same lane contribute `lane.m_length * abs(toOffset - fromOffset) / 255`.
- For a change from lane A to lane B, the calculator resolves A's target world position, uses vanilla `PathUnit.CalculatePathPositionOffset` to find the entry offset on B, adds the endpoint chord between them once, then adds only B's entry-to-target lane portion. This avoids counting all of either lane.
- The connector is an intentional approximation. Vehicle and citizen AIs build mode-specific Bezier control points at runtime; reconstructing those private curves would duplicate substantial AI logic. The chord preserves the correct endpoints and normally introduces only a small local error.
- During an even lane-travel phase, the entity's last-frame world position is projected onto the current lane with vanilla `CalculatePathPositionOffset`. Only the portion from that projected offset to the current target is counted.
- During an odd transition phase, the already-completed current-lane portion is omitted. The remaining connector chord starts at the entity's current world position, followed by the untraversed part of the next lane.
- An odd phase with no following position contributes zero: vanilla has reached the terminal target and is about to release or replace the path.

Every lane, segment, offset-derived position, and accumulated float is validated. Invalid, released, changing, cyclic, non-finite, or otherwise inconsistent state returns `false` instead of a partial result.

## Mutable-state and thread observations

- The chosen future binding callback is Unity's visible-panel `LateUpdate` path, i.e. the Unity/UI main thread.
- Vanilla `PathVisualizer` itself reads the same vehicle, citizen, path, segment, and lane buffers for display from a Unity component. This supports a small read-only UI-thread snapshot in normal CS1 practice.
- The buffers remain simulation-owned and mutable. Phase 3-7 code validates IDs before every chain dereference, copies structs, checks unit identity again after reading, and rejects the result if the selected entity's path/progress changes before completion. It does not lock game buffers, add path references, or mutate simulation state.

## Phase 1 exit-gate summary

| Required finding | Result |
|---|---|
| Vehicle panel class | `VehicleWorldInfoPanel` |
| Citizen panel class | `CitizenWorldInfoPanel` |
| Vehicle selected ID | `WorldInfoPanel.m_InstanceID.Vehicle` |
| Citizen selected ID | `m_InstanceID.Citizen` -> `Citizen.m_instance` |
| Vehicle/Citizen path | public `uint m_path`; zero means none |
| Current progress | decoded index plus even lane/odd connector phase; current world position is projected with vanilla helpers |
| Path layout/access | 12 inline positions, `GetPosition`, chained by `m_nextPathUnit` |
| Position to lane | `PathManager.GetLaneID(Position)` |
| Lane length | cached physical `NetLane.m_length`, built from lane Bezier |
| `PathUnit.m_length` | search bound, then mode-dependent method distance/duration; not physical route length |
| Panel integration | root component; status-label parent is confirmed content/style source |

All structural and Phase 5-7 calculation questions are answered for the targeted, locally installed **1.21.1-f9** assembly.
